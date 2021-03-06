﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Tangent.Intermediate.Interop
{
    public class DirectConstructorCallExpression : Expression
    {
        public readonly ConstructorInfo Constructor;
        public readonly IEnumerable<Expression> Arguments;

        public DirectConstructorCallExpression(ConstructorInfo ctor, IEnumerable<Expression> args) : base(null)
        {
            Constructor = ctor;
            Arguments = args;
        }

        public override TangentType EffectiveType
        {
            get
            {
                return DotNetType.For(Constructor.DeclaringType);
            }
        }

        public override ExpressionNodeType NodeType
        {
            get
            {
                return ExpressionNodeType.DirectConstructorCall;
            }
        }

        public override Expression ReplaceParameterAccesses(Dictionary<ParameterDeclaration, Expression> mapping)
        {
            var newbs = Arguments.Select(expr => expr.ReplaceParameterAccesses(mapping));
            if (Arguments.SequenceEqual(newbs)) {
                return this;
            }

            return new DirectConstructorCallExpression(Constructor, newbs);
        }

        public override bool RequiresClosureAround(HashSet<ParameterDeclaration> parameters, HashSet<Expression> workset)
        {
            if (workset.Contains(this)) {
                return false;
            }

            workset.Add(this);
            return Arguments.Any(arg => arg.RequiresClosureAround(parameters, workset));
        }

        public override bool AccessesAnyParameters(HashSet<ParameterDeclaration> parameters, HashSet<Expression> workset)
        {
            if (workset.Contains(this)) {
                return false;
            }

            workset.Add(this);

            return Arguments.Any(arg => arg.AccessesAnyParameters(parameters, workset));
        }
    }
}
