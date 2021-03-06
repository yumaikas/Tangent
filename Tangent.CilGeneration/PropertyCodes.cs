﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Tangent.CilGeneration
{
    public class PropertyCodes
    {
        public readonly Action<ILGenerator> Accessor;
        public readonly Action<ILGenerator, Action> Mutator;

        public PropertyCodes(Action<ILGenerator> accessor, Action<ILGenerator, Action> mutator)
        {
            Accessor = accessor;
            Mutator = mutator;
        }
    }
}
