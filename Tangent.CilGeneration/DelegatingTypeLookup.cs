﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Tangent.Intermediate;

namespace Tangent.CilGeneration
{
    public class DelegatingTypeLookup : ITypeLookup
    {
        private readonly ITypeCompiler typeCompiler;
        private readonly IEnumerable<TypeDeclaration> declaredTypes;
        private readonly Dictionary<TangentType, Type> lookup = new Dictionary<TangentType, Type>();

        public DelegatingTypeLookup(ITypeCompiler typeCompiler, IEnumerable<TypeDeclaration> declaredTypes)
        {
            this.typeCompiler = typeCompiler;
            this.declaredTypes = declaredTypes;
        }

        public Type this[TangentType t]
        {
            get
            {
                if (!lookup.ContainsKey(t)) {
                    PopulateLookupWith(t);
                }

                return lookup[t];
            }
        }

        public void AddGenericFunctionParameterMapping(ParameterDeclaration generic, GenericTypeParameterBuilder dotnetType)
        {
            var reference = GenericArgumentReferenceType.For(generic);
            var inference = GenericInferencePlaceholder.For(generic);
            if (!lookup.ContainsKey(reference)) {
                lookup.Add(reference, dotnetType);
            }

            if (!lookup.ContainsKey(inference)) {
                lookup.Add(inference, dotnetType);
            }
        }

        private void PopulateLookupWith(TangentType t)
        {
            switch (t.ImplementationType) {
                case KindOfType.Enum:
                case KindOfType.Product:
                case KindOfType.Sum:
                case KindOfType.BoundGeneric:
                    // This should already be declared in our types.
                    var result = declaredTypes.FirstOrDefault(td => td.Returns == t);
                    if (result == null) {
                        result = new TypeDeclaration((PhrasePart)null, t);
                    }

                    var type = typeCompiler.Compile(result, (placeholderTarget, placeholder) => { if (!lookup.ContainsKey(placeholderTarget)) { lookup.Add(placeholderTarget, placeholder); } }, (tt, create) => { if (create) { return this[tt]; } else { if (lookup.ContainsKey(tt)) { return lookup[tt]; } else { return null; } } });
                    if (lookup.ContainsKey(result.Returns)) {
                        lookup[result.Returns] = type;
                    } else {
                        lookup.Add(result.Returns, type);
                    }

                    return;

                case KindOfType.BoundGenericProduct:
                    var binding = t as BoundGenericProductType;
                    var genericType = lookup[binding.GenericProductType];
                    var arguments = binding.TypeArguments.Select(a => lookup[a]);
                    lookup.Add(t, genericType.MakeGenericType(arguments.ToArray()));
                    return;

                case KindOfType.Lazy:
                    // The target of the type constructor needs to be already declared in our types.
                    var lazyType = t as LazyType;
                    var target = this[lazyType.Type];

                    if (target == typeof(void)) {
                        lookup.Add(t, typeof(Action));
                    } else {
                        lookup.Add(t, typeof(Func<>).MakeGenericType(target));
                    }

                    return;

                case KindOfType.SingleValue:
                    throw new NotImplementedException("Something is asking for the type of a SingleValueType. We should never get here.");

                case KindOfType.Builtin:
                    lookup.Add(TangentType.Void, typeof(void));
                    lookup.Add(TangentType.String, typeof(string));
                    lookup.Add(TangentType.Int, typeof(int));
                    lookup.Add(TangentType.Double, typeof(double));
                    lookup.Add(TangentType.Bool, typeof(bool));
                    return;

                case KindOfType.Kind:
                    // For now, all we know is `kind of any`
                    if (t == TangentType.Any.Kind) {
                        lookup.Add(TangentType.Any.Kind, typeof(object));
                    } else {
                        throw new NotImplementedException();
                    }

                    return;
                    
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
