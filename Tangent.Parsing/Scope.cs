﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tangent.Intermediate;
using Tangent.Parsing.TypeResolved;

namespace Tangent.Parsing
{
    public class Scope
    {
        public readonly IEnumerable<ParameterDeclaration> Parameters;
        public readonly IEnumerable<TypeDeclaration> Types;
        public readonly IEnumerable<ReductionDeclaration> Functions;
        public readonly TangentType ReturnType;

        public Scope(TangentType returnType, IEnumerable<TypeDeclaration> types, IEnumerable<ParameterDeclaration> parameters, IEnumerable<ReductionDeclaration> functions)
        {
            ReturnType = returnType;
            Parameters = parameters.OrderByDescending(p => p.Takes.Count()).ToList();
            Types = types.OrderByDescending(t => t.Takes.Count()).ToList();
            Functions = new[]{ (ReturnType == TangentType.Void) ?
                    new ReductionDeclaration("return", BuiltinFunctions.Return) :
                    new ReductionDeclaration(new[] { new PhrasePart("return"), new PhrasePart(new ParameterDeclaration("value", ReturnType)) }, BuiltinFunctions.Return)}
                .Concat(functions.OrderByDescending(f => f.Takes.Count())).ToList();

        }
    }
}
