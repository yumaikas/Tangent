﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tangent.Parsing.Errors;
using Tangent.Tokenization;

namespace Tangent.Parsing
{
    public class SelectingParser<A, T> : Parser<T>
    {
        private readonly Parser<A> a;
        private readonly Func<A, T> selector;

        public SelectingParser(Parser<A> a, Func<A, T> selector)
        {
            this.a = a;
            this.selector = selector;
        }

        public override ResultOrParseError<T> Parse(IEnumerable<Token> tokens, out int consumed)
        {
            var first = a.Parse(tokens, out consumed);
            if (!first.Success) { return new ResultOrParseError<T>(first.Error); }

            return selector(first.Result);
        }
    }

    public class CombinedParser<A, B, T> : Parser<T>
    {
        private readonly Parser<A> a;
        private readonly Parser<B> b;
        private readonly Func<A, B, T> selector;

        public CombinedParser(Parser<A> a, Parser<B> b, Func<A, B, T> selector)
        {
            this.a = a;
            this.b = b;
            this.selector = selector;
        }

        public override ResultOrParseError<T> Parse(IEnumerable<Token> tokens, out int consumed)
        {
            int taken = 0;
            int last = 0;
            consumed = 0;
            var first = a.Parse(tokens, out taken);
            if (!first.Success) { return new ResultOrParseError<T>(first.Error); }
            var second = b.Parse(tokens.Skip(taken), out last);
            if (!second.Success) { return new ResultOrParseError<T>(second.Error); }
            taken += last;

            consumed = taken;
            return selector(first.Result, second.Result);
        }
    }

    public class CombinedParser<A, B, C, T> : Parser<T>
    {
        private readonly Parser<A> a;
        private readonly Parser<B> b;
        private readonly Parser<C> c;
        private readonly Func<A, B, C, T> selector;

        public CombinedParser(Parser<A> a, Parser<B> b, Parser<C> c, Func<A, B, C, T> selector)
        {
            this.a = a;
            this.b = b;
            this.c = c;
            this.selector = selector;
        }

        public override ResultOrParseError<T> Parse(IEnumerable<Token> tokens, out int consumed)
        {
            int taken = 0;
            int last = 0;
            consumed = 0;
            var first = a.Parse(tokens, out taken);
            if (!first.Success) { return new ResultOrParseError<T>(first.Error); }
            var second = b.Parse(tokens.Skip(taken), out last);
            if (!second.Success) { return new ResultOrParseError<T>(second.Error); }
            taken += last;
            var third = c.Parse(tokens.Skip(taken), out last);
            if (!third.Success) { return new ResultOrParseError<T>(third.Error); }
            taken += last;

            consumed = taken;
            return selector(first.Result, second.Result, third.Result);
        }
    }

    public class CombinedParser<A, B, C, D, T> : Parser<T>
    {
        private readonly Parser<A> a;
        private readonly Parser<B> b;
        private readonly Parser<C> c;
        private readonly Parser<D> d;
        private readonly Func<A, B, C, D, T> selector;

        public CombinedParser(Parser<A> a, Parser<B> b, Parser<C> c, Parser<D> d, Func<A, B, C, D, T> selector)
        {
            this.a = a;
            this.b = b;
            this.c = c;
            this.d = d;
            this.selector = selector;
        }

        public override ResultOrParseError<T> Parse(IEnumerable<Token> tokens, out int consumed)
        {
            int taken = 0;
            int last = 0;
            consumed = 0;
            var first = a.Parse(tokens, out taken);
            if (!first.Success) { return new ResultOrParseError<T>(first.Error); }
            var second = b.Parse(tokens.Skip(taken), out last);
            if (!second.Success) { return new ResultOrParseError<T>(second.Error); }
            taken += last;
            var third = c.Parse(tokens.Skip(taken), out last);
            if (!third.Success) { return new ResultOrParseError<T>(third.Error); }
            taken += last;
            var fourth = d.Parse(tokens.Skip(taken), out last);
            if (!fourth.Success) { return new ResultOrParseError<T>(fourth.Error); }
            taken += last;

            consumed = taken;
            return selector(first.Result, second.Result, third.Result, fourth.Result);
        }
    }

    public class CombinedParser<A, B, C, D, E, T> : Parser<T>
    {
        private readonly Parser<A> a;
        private readonly Parser<B> b;
        private readonly Parser<C> c;
        private readonly Parser<D> d;
        private readonly Parser<E> e;
        private readonly Func<A, B, C, D, E, T> selector;

        public CombinedParser(Parser<A> a, Parser<B> b, Parser<C> c, Parser<D> d, Parser<E> e, Func<A, B, C, D, E, T> selector)
        {
            this.a = a;
            this.b = b;
            this.c = c;
            this.d = d;
            this.e = e;
            this.selector = selector;
        }

        public override ResultOrParseError<T> Parse(IEnumerable<Token> tokens, out int consumed)
        {
            int taken = 0;
            int last = 0;
            consumed = 0;
            var first = a.Parse(tokens, out taken);
            if (!first.Success) { return new ResultOrParseError<T>(first.Error); }
            var second = b.Parse(tokens.Skip(taken), out last);
            if (!second.Success) { return new ResultOrParseError<T>(second.Error); }
            taken += last;
            var third = c.Parse(tokens.Skip(taken), out last);
            if (!third.Success) { return new ResultOrParseError<T>(third.Error); }
            taken += last;
            var fourth = d.Parse(tokens.Skip(taken), out last);
            if (!fourth.Success) { return new ResultOrParseError<T>(fourth.Error); }
            taken += last;
            var fifth = e.Parse(tokens.Skip(taken), out last);
            if (!fifth.Success) { return new ResultOrParseError<T>(fifth.Error); }
            taken += last;

            consumed = taken;
            return selector(first.Result, second.Result, third.Result, fourth.Result, fifth.Result);
        }
    }

    public class CombinedParser<A, B, C, D, E,F, T> : Parser<T>
    {
        private readonly Parser<A> a;
        private readonly Parser<B> b;
        private readonly Parser<C> c;
        private readonly Parser<D> d;
        private readonly Parser<E> e;
        private readonly Parser<F> f;
        private readonly Func<A, B, C, D, E, F, T> selector;

        public CombinedParser(Parser<A> a, Parser<B> b, Parser<C> c, Parser<D> d, Parser<E> e, Parser<F> f, Func<A, B, C, D, E,F, T> selector)
        {
            this.a = a;
            this.b = b;
            this.c = c;
            this.d = d;
            this.e = e;
            this.f = f;
            this.selector = selector;
        }

        public override ResultOrParseError<T> Parse(IEnumerable<Token> tokens, out int consumed)
        {
            int taken = 0;
            int last = 0;
            consumed = 0;
            var first = a.Parse(tokens, out taken);
            if (!first.Success) { return new ResultOrParseError<T>(first.Error); }
            var second = b.Parse(tokens.Skip(taken), out last);
            if (!second.Success) { return new ResultOrParseError<T>(second.Error); }
            taken += last;
            var third = c.Parse(tokens.Skip(taken), out last);
            if (!third.Success) { return new ResultOrParseError<T>(third.Error); }
            taken += last;
            var fourth = d.Parse(tokens.Skip(taken), out last);
            if (!fourth.Success) { return new ResultOrParseError<T>(fourth.Error); }
            taken += last;
            var fifth = e.Parse(tokens.Skip(taken), out last);
            if (!fifth.Success) { return new ResultOrParseError<T>(fifth.Error); }
            taken += last;
            var sixth = f.Parse(tokens.Skip(taken), out last);
            if (!sixth.Success) { return new ResultOrParseError<T>(sixth.Error); }
            taken += last;

            consumed = taken;
            return selector(first.Result, second.Result, third.Result, fourth.Result, fifth.Result, sixth.Result);
        }
    }
}
