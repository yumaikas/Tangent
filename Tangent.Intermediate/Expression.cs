﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tangent.Intermediate {
    public enum ExpressionNodeType {
        Unknown = 0,
        Identifier = 1,
        ParameterAccess = 2,
        FunctionBinding = 3,
        FunctionInvocation = 4,
        TypeAccess = 5,
    }

    public abstract class Expression {
        public abstract ExpressionNodeType NodeType { get; }
    }
}