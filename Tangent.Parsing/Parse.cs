﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tangent.Intermediate;
using Tangent.Intermediate.Interop;
using Tangent.Parsing.Errors;
using Tangent.Parsing.Partial;
using Tangent.Parsing.TypeResolved;
using Tangent.Tokenization;

namespace Tangent.Parsing
{
    public static class Parse
    {
        public static ResultOrParseError<TangentProgram> TangentProgram(IEnumerable<Token> tokens, ImportBundle imports = null)
        {
            imports = imports ?? ImportBundle.Empty;
            return TangentProgram(new List<Token>(tokens), imports);
        }

        private static ResultOrParseError<TangentProgram> TangentProgram(List<Token> tokens, ImportBundle imports)
        {
            if (!tokens.Any()) {
                return new TangentProgram(Enumerable.Empty<TypeDeclaration>(), Enumerable.Empty<ReductionDeclaration>(), Enumerable.Empty<Field>(), Enumerable.Empty<string>());
            }

            List<string> inputSources = tokens.Select(t => t.SourceInfo.Label).Distinct().ToList();
            List<PartialTypeDeclaration> partialTypes = new List<PartialTypeDeclaration>();
            List<PartialReductionDeclaration> partialFunctions = new List<PartialReductionDeclaration>();
            List<PartialInterfaceBinding> partialStandaloneInterfaceBindings = new List<PartialInterfaceBinding>();
            List<InterfaceBinding> interfaceToImplementerBindings = new List<InterfaceBinding>();
            List<VarDeclElement> parsedGlobalFields = new List<VarDeclElement>();

            while (tokens.Any()) {
                int typeTake;
                var type = Grammar.TypeDecl.Parse(tokens, out typeTake);
                int fnTake;
                int ifTake;
                int fldTake;
                if (type.Success) {
                    partialTypes.Add(type.Result);
                    partialFunctions.AddRange(ExtractPartialFunctions(type.Result.Returns));
                    tokens.RemoveRange(0, typeTake);
                } else {
                    var fn = Grammar.FunctionDeclaration.Parse(tokens, out fnTake);
                    if (fn.Success) {
                        partialFunctions.Add(fn.Result);
                        tokens.RemoveRange(0, fnTake);
                    } else {
                        var binding = Grammar.StandaloneInterfaceBinding.Parse(tokens, out ifTake);
                        if (binding.Success) {
                            partialStandaloneInterfaceBindings.Add(binding.Result);
                            partialFunctions.AddRange(binding.Result.Functions);
                            tokens.RemoveRange(0, ifTake);
                        } else {
                            //
                            // No global state.
                            // 
                            //var field = Grammar.FieldDeclaration.Parse(tokens, out fldTake);
                            //if (field.Success) {
                            //    parsedGlobalFields.Add(field.Result);
                            //    tokens.RemoveRange(0, fldTake);
                            //} else {
                            //    return new ResultOrParseError<TangentProgram>(typeTake >= fnTake ? type.Error : fn.Error);
                            //}
                        }
                    }
                }
            }

            List<TypeDeclaration> builtInTypes = new List<TypeDeclaration>(){
                new TypeDeclaration("void", TangentType.Void),
                new TypeDeclaration("int", TangentType.Int),
                new TypeDeclaration("double", TangentType.Double),
                new TypeDeclaration("bool", TangentType.Bool),
                new TypeDeclaration("string", TangentType.String),
                new TypeDeclaration("any", TangentType.Any),
                new TypeDeclaration("null", TangentType.Null)
            };

            Dictionary<PartialParameterDeclaration, ParameterDeclaration> genericArgumentMapping;
            var typeResult = TypeResolve.AllPartialTypeDeclarations(partialTypes, builtInTypes, out genericArgumentMapping);
            if (!typeResult.Success) {
                return new ResultOrParseError<TangentProgram>(typeResult.Error);
            }

            var types = typeResult.Result.Concat(imports.TypeDeclarations);

            var globalFields = TypeResolve.AllGlobalFields(parsedGlobalFields, types);
            if (!globalFields.Success) {
                return new ResultOrParseError<TangentProgram>(globalFields.Error);
            }

            // Move to Phase 2 - Resolve types in parameters and function return types.
            Dictionary<TangentType, TangentType> conversions;
            IEnumerable<ReductionDeclaration> delegateInvokers;
            var resolvedTypes = TypeResolve.AllTypePlaceholders(types, genericArgumentMapping, interfaceToImplementerBindings, partialStandaloneInterfaceBindings, out conversions, out delegateInvokers);
            if (!resolvedTypes.Success) {
                return new ResultOrParseError<TangentProgram>(resolvedTypes.Error);
            }

            var resolvedFunctions = TypeResolve.AllPartialFunctionDeclarations(partialFunctions, resolvedTypes.Result, conversions);
            if (!resolvedFunctions.Success) {
                return new ResultOrParseError<TangentProgram>(resolvedFunctions.Error);
            }


            HashSet<ProductType> allProductTypes = new HashSet<ProductType>();
            foreach (var t in resolvedTypes.Result) {
                if (t.Returns.ImplementationType == KindOfType.Product) {
                    allProductTypes.Add((ProductType)t.Returns);
                } else if (t.Returns.ImplementationType == KindOfType.Sum) {
                    allProductTypes.UnionWith(((SumType)t.Returns).Types.Where(tt => tt.ImplementationType == KindOfType.Product).Cast<ProductType>());
                }
            }

            HashSet<SumType> allSumTypes = new HashSet<SumType>(resolvedTypes.Result.Where(t => t.Returns.ImplementationType == KindOfType.Sum).Select(t => t.Returns).Cast<SumType>());

            //var ctorCalls = allProductTypes.Select(pt => new ReductionDeclaration(pt.DataConstructorParts, new CtorCall(pt), pt.DataConstructorParts.SelectMany(pp => pp.IsIdentifier ? Enumerable.Empty<ParameterDeclaration>() : pp.Parameter.Returns.ContainedGenericReferences(GenericTie.Inference)))).ToList();
            //foreach (var sum in allSumTypes) {
            //    foreach (var entry in sum.Types) {
            //        ctorCalls.Add(new ReductionDeclaration(new PhrasePart(new ParameterDeclaration("_", entry)), new CtorCall(sum)));
            //    }
            //}

            var ctorCalls = allProductTypes.Select(pt => GenerateConstructorFunctionFor(pt)).ToList();
            foreach (var sum in allSumTypes) {
                foreach (var entry in sum.Types) {
                    var valueParam = new ParameterDeclaration("_", entry);
                    ctorCalls.Add(new ReductionDeclaration(new PhrasePart(valueParam), new CtorCallExpression(sum, valueParam).GenerateWrappedFunction()));
                }
            }

            ctorCalls = ctorCalls.Concat(interfaceToImplementerBindings.Select(itoi => new ReductionDeclaration(new PhrasePart(new ParameterDeclaration("_", itoi.Implementation)), new InterfaceUpcast(itoi.Interface)))).ToList();

            var enumAccesses = resolvedTypes.Result.Where(tt => tt.Returns.ImplementationType == KindOfType.Enum).Select(tt => tt.Returns).Cast<EnumType>().SelectMany(tt => tt.Values.Select(v => new ReductionDeclaration(v, new Function(tt, new Block(new[] { new EnumValueAccessExpression(tt.SingleValueTypeFor(v), null) }, Enumerable.Empty<ParameterDeclaration>()))))).ToList();
            var fieldFunctions = new List<ReductionDeclaration>();
            Action<Field, ProductType> fieldFunctionizer = (field, productType) => {
                fieldFunctions.Add(new ReductionDeclaration(field.Declaration.Takes.Select(pp => pp.IsIdentifier ? pp.Identifier : new PhrasePart(new ParameterDeclaration("this", productType))), new Function(field.Declaration.Returns, new Block(new Expression[] { new FieldAccessorExpression(productType, field) }, Enumerable.Empty<ParameterDeclaration>()))));
                fieldFunctions.Add(new ReductionDeclaration(field.Declaration.Takes.Select(pp => pp.IsIdentifier ? pp.Identifier : new PhrasePart(new ParameterDeclaration("this", productType))).Concat(
                    new[] { new PhrasePart("="), new PhrasePart(new ParameterDeclaration("value", field.Declaration.Returns)) }), new Function(TangentType.Void, new Block(new Expression[] { new FieldMutatorExpression(productType, field) }, Enumerable.Empty<ParameterDeclaration>()))));

            };

            foreach (var field in globalFields.Result) {
                fieldFunctionizer(field, null);
            }

            foreach (var productType in allProductTypes) {
                foreach (var field in productType.Fields) {
                    fieldFunctionizer(field, productType);
                }
            }

            resolvedFunctions = new ResultOrParseError<IEnumerable<ReductionDeclaration>>(resolvedFunctions.Result.Concat(fieldFunctions).Concat(delegateInvokers).Concat(imports.Functions));

            // And now Phase 3 - Statement parsing based on syntax.
            var lookup = new Dictionary<Function, Function>();
            var errors = new List<ParseError>();
            resolvedFunctions = new ResultOrParseError<IEnumerable<ReductionDeclaration>>(resolvedFunctions.Result.Concat(BuiltinFunctions.All).Concat(enumAccesses));
            resolvedFunctions = FanOutFunctionsWithSumTypes(resolvedFunctions.Result);
            if (!resolvedFunctions.Success) { return new ResultOrParseError<TangentProgram>(resolvedFunctions.Error); }

            foreach (var fn in resolvedFunctions.Result) {
                TypeResolvedFunction partialFunction = fn.Returns as TypeResolvedFunction;
                if (partialFunction != null) {
                    if (partialFunction.Scope is TypeClass) {
                        lookup.Add(partialFunction, new InterfaceFunction(partialFunction.Scope as TypeClass, partialFunction.EffectiveType));
                    } else {
                        var locals = BuildLocals(partialFunction.Implementation.Locals, resolvedTypes.Result, errors).ToList();
                        var scope = new TransformationScope(((IEnumerable<TransformationRule>)resolvedTypes.Result.Select(td => new TypeAccess(td)))
                            .Concat(fn.Takes.Where(pp => !pp.IsIdentifier).Select(pp => new ParameterAccess(pp.Parameter)))
                            .Concat((partialFunction.Scope as ProductType) != null ? ConstructorParameterAccess.For(fn.Takes.First(pp => !pp.IsIdentifier && pp.Parameter.Takes.Count == 1 && pp.Parameter.IsThisParam).Parameter, (partialFunction.Scope as ProductType).DataConstructorParts.Where(pp => !pp.IsIdentifier).Select(pp => pp.Parameter)) : Enumerable.Empty<TransformationRule>())
                            .Concat(resolvedFunctions.Result.Concat(ctorCalls).Select(f => new FunctionInvocation(f)))
                            .Concat(new TransformationRule[] { LazyOperator.Common, SingleValueAccessor.Common, Delazy.Common }))
                            .CreateNestedScope(locals);

                        Function newb = BuildBlock(scope, types, partialFunction.EffectiveType, partialFunction.Implementation, locals, errors);

                        lookup.Add(partialFunction, newb);
                    }
                }
            }

            Func<Field, ProductType, Func<Expression, Expression>> fieldInitializerResolver = (field, productType) => (placeholder) => {
                var castPlaceholder = placeholder as InitializerPlaceholder;
                TransformationScope scope = null;

                if (productType == null) {
                    scope = new TransformationScope(((IEnumerable<TransformationRule>)resolvedTypes.Result.Select(td => new TypeAccess(td)))
                            .Concat(resolvedFunctions.Result.Concat(ctorCalls).Select(f => new FunctionInvocation(f)))
                            .Concat(new TransformationRule[] { LazyOperator.Common, SingleValueAccessor.Common, Delazy.Common }));
                } else {
                    // Initializers can't use locals (directly), and don't have parameters.
                    scope = new TransformationScope(((IEnumerable<TransformationRule>)resolvedTypes.Result.Select(td => new TypeAccess(td)))
                            .Concat(field.Declaration.Returns is DelegateType ? (IEnumerable<TransformationRule>)new[] { new TypeAccess(new TypeDeclaration("this", productType)) } : Enumerable.Empty<TransformationRule>())
                            .Concat(resolvedFunctions.Result.Concat(ctorCalls).Select(f => new FunctionInvocation(f)))
                            .Concat(ConstructorParameterAccess.For(field.Declaration.Takes.First(pp => !pp.IsIdentifier).Parameter, productType.DataConstructorParts.Where(pp => !pp.IsIdentifier).Select(pp => pp.Parameter)))
                            .Concat(new TransformationRule[] { LazyOperator.Common, SingleValueAccessor.Common, Delazy.Common }));
                }

                var expr = castPlaceholder.UnresolvedInitializer.FlatTokens.Select(pe => ElementToExpression(scope, types, pe, errors)).ToList();
                var result = scope.InterpretTowards(field.Declaration.Returns, expr);

                if (result.Count == 0) {
                    errors.Add(new IncomprehensibleStatementError(expr));
                    return placeholder;
                } else if (result.Count > 1) {
                    errors.Add(new AmbiguousStatementError(expr, result));
                    return placeholder;
                } else {
                    return result.First();
                }
            };

            foreach (var field in globalFields.Result) {
                field.ResolveInitializerPlaceholders(fieldInitializerResolver(field, null));
                // TODO: reorder fields based on initialization dependencies (or error).
            }

            foreach (var productType in allProductTypes) {
                foreach (var field in productType.Fields) {
                    field.ResolveInitializerPlaceholders(fieldInitializerResolver(field, productType));
                }

                // TODO: reorder fields based on initialization dependencies (or error).
            }

            if (errors.Any()) {
                return new ResultOrParseError<TangentProgram>(new AggregateParseError(errors));
            }

            // 3a - Replace TypeResolvedFunctions with fully resolved ones.
            var workset = new HashSet<Expression>();
            foreach (var fn in lookup.Values) {
                fn.ReplaceTypeResolvedFunctions(lookup, workset);
            }

            return new TangentProgram(resolvedTypes.Result, resolvedFunctions.Result.Select(fn => {
                if (fn.Returns is TypeResolvedFunction) {
                    var trf = fn.Returns as TypeResolvedFunction;
                    if (trf.Scope is TypeClass) {
                        return new ReductionDeclaration(fn.Takes, new InterfaceFunction(trf.Scope as TypeClass, fn.Returns.EffectiveType), fn.GenericParameters);
                    } else {
                        return new ReductionDeclaration(fn.Takes, lookup[fn.Returns], fn.GenericParameters);
                    }

                } else {
                    return fn;
                }
            }).ToList(), globalFields.Result, inputSources);
        }

        private static IEnumerable<ParameterDeclaration> BuildLocals(IEnumerable<VarDeclElement> locals, IEnumerable<TypeDeclaration> types, List<ParseError> errors)
        {
            foreach (var entry in locals) {
                var decl = TypeResolve.Resolve(entry.ParameterDeclaration, types);
                if (!decl.Success) {
                    errors.Add(decl.Error);
                } else {
                    yield return decl.Result;
                }
            }
        }

        private static Function BuildBlock(TransformationScope scope, IEnumerable<TypeDeclaration> types, TangentType effectiveType, PartialBlock partialBlock, IEnumerable<ParameterDeclaration> locals, List<ParseError> errors)
        {
            var block = BuildBlock(scope, types, effectiveType, partialBlock.Statements, locals, errors);

            return new Function(effectiveType, block);
        }

        private static Block BuildBlock(TransformationScope scope, IEnumerable<TypeDeclaration> types, TangentType effectiveType, IEnumerable<PartialStatement> elements, IEnumerable<ParameterDeclaration> locals, List<ParseError> errors)
        {
            List<Expression> statements = new List<Expression>();
            if (!elements.Any()) {
                if (effectiveType != TangentType.Void) { errors.Add(new IncomprehensibleStatementError(Enumerable.Empty<Expression>())); }
                return new Block(Enumerable.Empty<Expression>(), Enumerable.Empty<ParameterDeclaration>());
            }

            var allElements = elements.ToList();
            for (int ix = 0; ix < allElements.Count; ++ix) {
                var line = allElements[ix];
                var statementBits = line.FlatTokens.Select(t => ElementToExpression(scope, types, t, errors)).ToList();
                var statement = scope.InterpretTowards((effectiveType != null && ix == allElements.Count - 1) ? effectiveType : TangentType.Void, statementBits);
                if (statement.Count == 0) {
                    errors.Add(new IncomprehensibleStatementError(statementBits));
                } else if (statement.Count > 1) {
                    errors.Add(new AmbiguousStatementError(statementBits, statement));
                } else {
                    statements.Add(statement.First());
                }
            }

            return new Block(statements, locals);
        }

        private static Expression ElementToExpression(TransformationScope scope, IEnumerable<TypeDeclaration> types, PartialElement element, List<ParseError> errors)
        {
            switch (element.Type) {
                case ElementType.Identifier:
                    return new IdentifierExpression(((IdentifierElement)element).Identifier, element.SourceInfo);
                case ElementType.Block:
                    var stmts = ((BlockElement)element).Block.Statements.ToList();
                    var locals = BuildLocals(((BlockElement)element).Block.Locals, types, errors);
                    if (locals.Any()) {
                        scope = scope.CreateNestedScope(locals);
                    }
                    var last = stmts.Last();
                    stmts.RemoveAt(stmts.Count - 1);
                    var notLast = BuildBlock(scope, types, null, stmts, locals, errors);
                    var lastExpr = last.FlatTokens.Select(e => ElementToExpression(scope, types, e, errors)).ToList();
                    var info = lastExpr.Aggregate((LineColumnRange)null, (a, expr) => expr.SourceInfo.Combine(a));
                    info = notLast.Statements.Any() ? notLast.Statements.Aggregate(info, (a, stmt) => a.Combine(stmt.SourceInfo)) : info;
                    return new ParenExpression(notLast, lastExpr, info);
                case ElementType.Constant:
                    return ((ConstantElement)element).TypelessExpression;
                case ElementType.Lambda:
                    var concrete = (LambdaElement)element;
                    return new PartialLambdaExpression(concrete.Takes.Select(vde => VarDeclToParameterDeclaration(scope, vde, errors)).ToList(), scope, (newScope, returnType) => {
                        var lambdaErrors = new List<ParseError>();
                        if (concrete.Body.Block.Locals.Any()) {
                            throw new NotImplementedException("TODO: make locals in lambdas work.");
                        }
                        var implementation = BuildBlock(newScope, types, returnType, concrete.Body.Block, Enumerable.Empty<ParameterDeclaration>(), lambdaErrors);
                        if (lambdaErrors.Where(e => !(e is AmbiguousStatementError)).Any()) {
                            return null;
                        }

                        if (lambdaErrors.Where(e => e is AmbiguousStatementError).Any()) {
                            return new AmbiguousExpression(lambdaErrors.Where(e => e is AmbiguousStatementError).Select(e => e as AmbiguousStatementError).SelectMany(a => a.PossibleInterpretations));
                        }

                        // RMS: being lazy. Should probably have an Either or a BlockExpr.
                        return new ParenExpression(implementation.Implementation, null, concrete.Body.SourceInfo);
                    }, element.SourceInfo);
                default:
                    throw new NotImplementedException();
            }
        }

        private static ParameterDeclaration VarDeclToParameterDeclaration(TransformationScope scope, VarDeclElement vde, List<ParseError> error)
        {
            if (!vde.ParameterDeclaration.Takes.All(ppp => ppp.IsIdentifier)) {
                throw new NotImplementedException("Parameterized variable declarations not currently supported.");
            }

            var result = vde.ParameterDeclaration.Returns == null ? new ParameterDeclaration(vde.ParameterDeclaration.Takes.Select(ppp => new PhrasePart(ppp.Identifier.Identifier)), null) :
                TypeResolve.Resolve(vde.ParameterDeclaration, scope.Rules.SelectMany(x => x).Where(r => r.Type == TransformationType.Type).Cast<TypeAccess>().Select(t => t.Declaration));
            if (result.Success) {
                return result.Result;
            } else {
                error.Add(new IncomprehensibleStatementError(vde.ParameterDeclaration.Returns));
                return null;
            }
        }


        private static IEnumerable<PartialReductionDeclaration> ExtractPartialFunctions(TangentType tt, HashSet<TangentType> searched = null)
        {
            searched = searched ?? new HashSet<TangentType>();
            switch (tt.ImplementationType) {
                case KindOfType.Sum:
                    if (searched.Contains(tt)) {
                        return Enumerable.Empty<PartialReductionDeclaration>();
                    }

                    searched.Add(tt);
                    List<PartialReductionDeclaration> result = new List<PartialReductionDeclaration>();
                    foreach (var part in ((SumType)tt).Types) {
                        result.AddRange(ExtractPartialFunctions(part, searched));
                    }

                    return result;
                case KindOfType.Placeholder:
                    if (tt is PartialProductType) {
                        return ((PartialProductType)tt).Functions;
                    }

                    if (tt is PartialInterface) {
                        return ((PartialInterface)tt).Functions;
                    }

                    return Enumerable.Empty<PartialReductionDeclaration>();
                default:
                    return Enumerable.Empty<PartialReductionDeclaration>();
            }
        }


        private static ResultOrParseError<IEnumerable<ReductionDeclaration>> FanOutFunctionsWithSumTypes(IEnumerable<ReductionDeclaration> resolvedFunctions)
        {
            IFunctionSignatureSet fss = new TieredFunctionSignatureSet();
            fss.AddRange(resolvedFunctions);
            for (int ix = 0; ix < fss.Functions.Count; ++ix) {
                var entry = fss.Functions[ix];

                List<List<PhrasePart>> parts = entry.Takes.Select(pp => {
                    if (!pp.IsIdentifier && pp.Parameter.Returns.ImplementationType == KindOfType.Sum) {
                        return ((SumType)pp.Parameter.Returns).Types.Select(tt => new PhrasePart(new ParameterDeclaration(pp.Parameter.Takes, tt))).ToList();
                    } else if (!pp.IsIdentifier && pp.Parameter.Returns.ImplementationType == KindOfType.BoundGeneric) {
                        var conc = pp.Parameter.Returns;
                        while (conc.ImplementationType == KindOfType.BoundGeneric) {
                            conc = ((BoundGenericType)conc).ConcreteType;
                        }

                        if (conc.ImplementationType == KindOfType.Sum) {
                            return ((SumType)conc).Types.Select(tt => new PhrasePart(new ParameterDeclaration(pp.Parameter.Takes, tt))).ToList();
                        }

                        return new List<PhrasePart>() { pp };

                    } else {
                        return new List<PhrasePart>() { pp };
                    }
                }).ToList();

                if (!parts.All(p => p.Count == 1)) {
                    foreach (var variant in parts.GetCombos()) {
                        var trf = entry.Returns as TypeResolvedFunction;
                        var parameterGenerics = variant.SelectMany(pp => pp.IsIdentifier ? Enumerable.Empty<ParameterDeclaration>() : pp.Parameter.Returns.ContainedGenericReferences(GenericTie.Inference)).ToList();
                        var returnGenericsTiedToInference = (trf != null ? trf.EffectiveType : entry.Returns.EffectiveType).ContainedGenericReferences(GenericTie.Reference).Where(pd => entry.GenericParameters.Contains(pd));
                        var badGenerics = returnGenericsTiedToInference.Where(pd => !parameterGenerics.Contains(pd)).ToList();
                        if (badGenerics.Count == 1) {
                            return new ResultOrParseError<IEnumerable<ReductionDeclaration>>(new GenericSumTypeFunctionWithReturnTypeRelyingOnInference(variant, badGenerics[0]));
                        }

                        if (badGenerics.Count > 1) {
                            return new ResultOrParseError<IEnumerable<ReductionDeclaration>>(new AggregateParseError(badGenerics.Select(bg => new GenericSumTypeFunctionWithReturnTypeRelyingOnInference(variant, bg))));
                        }
                        ReductionDeclaration newb;
                        if (trf != null) {
                            newb = new ReductionDeclaration(variant, new TypeResolvedFunction(trf.EffectiveType, trf.Implementation, trf.Scope), parameterGenerics);
                        } else if (entry.Returns.GetType() == typeof(Function)) {
                            var parameterMapping = variant.Zip(entry.Takes, (a, b) => Tuple.Create(a, b)).Where(p => !p.Item1.IsIdentifier).ToDictionary(p => p.Item2.Parameter, p => (Expression)new ParameterAccessExpression(p.Item1.Parameter, null));

                            newb = new ReductionDeclaration(variant, new Function(entry.Returns.EffectiveType, entry.Returns.Implementation.ReplaceParameterAccesses(parameterMapping)), parameterGenerics);
                        } else {
                            throw new NotImplementedException();
                        }

                        fss.Add(newb);
                    }
                }
            }

            return fss.Functions;
        }

        private static ReductionDeclaration GenerateConstructorFunctionFor(ProductType pt)
        {
            var genericMapping = pt.DataConstructorParts.SelectMany(pp => pp.IsIdentifier ? Enumerable.Empty<ParameterDeclaration>() : pp.Parameter.Returns.ContainedGenericReferences(GenericTie.Inference)).ToDictionary(gen => gen, gen => GenericInferencePlaceholder.For(new ParameterDeclaration(new[] { new PhrasePart("ctor") }.Concat(gen.Takes), gen.Returns)));

            Func<PhrasePart, PhrasePart> fixer = null;
            fixer = pp => {
                if (pp.IsIdentifier) { return pp; }
                return new PhrasePart(new ParameterDeclaration(pp.Parameter.Takes.Select(inner => fixer(inner)), pp.Parameter.Returns.RebindInferences(gen => genericMapping[gen])));
            };

            var targetType = pt.RebindInferences(gen => GenericArgumentReferenceType.For(genericMapping[gen].GenericArgument));
            Dictionary<ParameterDeclaration, ParameterDeclaration> paramMapping = new Dictionary<ParameterDeclaration, ParameterDeclaration>();
            Func<PhrasePart, PhrasePart> paramMapper = pp => {
                if (pp.IsIdentifier) {
                    return pp;
                }

                var newb = new PhrasePart(new ParameterDeclaration(pp.Parameter.Takes.Select(inner => fixer(inner)), pp.Parameter.Returns.RebindInferences(gen => genericMapping[gen])));
                paramMapping.Add(pp.Parameter, newb.Parameter);
                return newb;
            };

            if (targetType is ProductType) {
                return new ReductionDeclaration(pt.DataConstructorParts.Select(pp => paramMapper(pp)), new Function(targetType, new Block(new Expression[] { new CtorCallExpression(targetType as ProductType, pd => paramMapping[pd]) }, Enumerable.Empty<ParameterDeclaration>())));
            } else if (targetType is BoundGenericProductType) {
                return new ReductionDeclaration(pt.DataConstructorParts.Select(pp => paramMapper(pp)), new Function(targetType, new Block(new Expression[] { new CtorCallExpression(targetType as BoundGenericProductType, pd => paramMapping[pd]) }, Enumerable.Empty<ParameterDeclaration>())), genericMapping.Values.Select(gip => gip.GenericArgument).ToList());
            } else {
                throw new NotImplementedException();
            }
        }
    }
}
