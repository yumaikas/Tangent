﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tangent.Intermediate.UnitTests
{
    [TestClass]
    public class GenericInferenceTests
    {
        [TestMethod]
        public void SimplePath()
        {
            var genericParam = new ParameterDeclaration("T", TangentType.Any);
            var inferencePlaceholder = GenericInferencePlaceholder.For(genericParam);
            var results = new Dictionary<ParameterDeclaration, TangentType>();

            Assert.IsTrue(inferencePlaceholder.CompatibilityMatches(TangentType.Int, results));
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(TangentType.Int, results[genericParam]);
        }

        [TestMethod]
        public void SimpleGenericPath()
        {
            // List<(T:any)> vs List<int> infers int properly.
            var genericParam = new ParameterDeclaration("T", TangentType.Any);
            var inferencePlaceholder = GenericInferencePlaceholder.For(genericParam);
            var T = new ParameterDeclaration("T", TangentType.Any.Kind);
            var genericList = new TypeDeclaration(new PhrasePart[] { new PhrasePart("List"), new PhrasePart(T) }, new ProductType(new[] { new PhrasePart(new ParameterDeclaration("obj", GenericArgumentReferenceType.For(T))) }, new[] { T }, Enumerable.Empty<Field>()));
            var listInt = BoundGenericProductType.For((ProductType)genericList.Returns, new[] { TangentType.Int });
            var listInferT = BoundGenericProductType.For((ProductType)genericList.Returns, new[] { inferencePlaceholder });
            var results = new Dictionary<ParameterDeclaration, TangentType>();

            Assert.IsTrue(listInferT.CompatibilityMatches(listInt, results));
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(TangentType.Int, results[genericParam]);
        }

        [TestMethod]
        public void SimpleGenericPathDoesNotCareAboutArgNames()
        {
            // List<(T:any)> vs List<int> infers int properly.
            var genericParam = new ParameterDeclaration("TT", TangentType.Any);
            var inferencePlaceholder = GenericInferencePlaceholder.For(genericParam);
            var T = new ParameterDeclaration("T", TangentType.Any.Kind);
            var genericList = new TypeDeclaration(new PhrasePart[] { new PhrasePart("List"), new PhrasePart(T) }, new ProductType(new[] { new PhrasePart(new ParameterDeclaration("obj", GenericArgumentReferenceType.For(T))) }, new[] { T }, Enumerable.Empty<Field>()));
            var listInt = BoundGenericProductType.For((ProductType)genericList.Returns, new[] { TangentType.Int });
            var listInferT = BoundGenericProductType.For((ProductType)genericList.Returns, new[] { inferencePlaceholder });
            var results = new Dictionary<ParameterDeclaration, TangentType>();

            Assert.IsTrue(listInferT.CompatibilityMatches(listInt, results));
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(TangentType.Int, results[genericParam]);
        }

        [TestMethod]
        public void MultiParamGenericPath()
        {
            // Map<(K:Any), (V:Any)> vs Map<int, string> infers both.
            var genericParam1 = new ParameterDeclaration("K", TangentType.Any);
            var genericParam2 = new ParameterDeclaration("V", TangentType.Any);
            var inferencePlaceholder1 = GenericInferencePlaceholder.For(genericParam1);
            var inferencePlaceholder2 = GenericInferencePlaceholder.For(genericParam2);
            var K = new ParameterDeclaration("typeK", TangentType.Any.Kind);
            var V = new ParameterDeclaration("typeV", TangentType.Any.Kind);
            var genericMap = new TypeDeclaration(new PhrasePart[] { new PhrasePart("Map"), new PhrasePart(K), new PhrasePart(V) }, new ProductType(new[] { new PhrasePart(new ParameterDeclaration("key", GenericArgumentReferenceType.For(K))), new PhrasePart(new ParameterDeclaration("value", GenericArgumentReferenceType.For(V))) }, new[] { K, V }, Enumerable.Empty<Field>()));
            var mapIntString = BoundGenericProductType.For((ProductType)genericMap.Returns, new[] { TangentType.Int, TangentType.String });
            var mapInfer = BoundGenericProductType.For((ProductType)genericMap.Returns, new[] { inferencePlaceholder1, inferencePlaceholder2 });
            var results = new Dictionary<ParameterDeclaration, TangentType>();

            Assert.IsTrue(mapInfer.CompatibilityMatches(mapIntString, results));
            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(TangentType.Int, results[genericParam1]);
            Assert.AreEqual(TangentType.String, results[genericParam2]);
        }

        [TestMethod]
        public void MultiParamGenericPathOrderMatters()
        {
            var genericParam1 = new ParameterDeclaration("K", TangentType.Any);
            var genericParam2 = new ParameterDeclaration("V", TangentType.Any);
            var inferencePlaceholder1 = GenericInferencePlaceholder.For(genericParam1);
            var inferencePlaceholder2 = GenericInferencePlaceholder.For(genericParam2);
            var K = new ParameterDeclaration("typeK", TangentType.Any.Kind);
            var V = new ParameterDeclaration("typeV", TangentType.Any.Kind);
            var genericMap = new TypeDeclaration(new PhrasePart[] { new PhrasePart("Map"), new PhrasePart(K), new PhrasePart(V) }, new ProductType(new[] { new PhrasePart(new ParameterDeclaration("key", GenericArgumentReferenceType.For(K))), new PhrasePart(new ParameterDeclaration("value", GenericArgumentReferenceType.For(V))) }, new[] { K, V }, Enumerable.Empty<Field>()));
            var mapIntString = BoundGenericProductType.For((ProductType)genericMap.Returns, new[] { TangentType.Int, TangentType.String });
            var mapInfer = BoundGenericProductType.For((ProductType)genericMap.Returns, new[] { inferencePlaceholder2, inferencePlaceholder1 });
            var results = new Dictionary<ParameterDeclaration, TangentType>();

            Assert.IsTrue(mapInfer.CompatibilityMatches(mapIntString, results));
            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(TangentType.Int, results[genericParam2]);
            Assert.AreEqual(TangentType.String, results[genericParam1]);
        }

        [TestMethod]
        public void NestedGenericPath()
        {
            // List<List<(T:any)>> vs List<List<int>> infers int properly.
            var genericParam = new ParameterDeclaration("T", TangentType.Any);
            var inferencePlaceholder = GenericInferencePlaceholder.For(genericParam);
            var T = new ParameterDeclaration("T", TangentType.Any.Kind);
            var genericList = new TypeDeclaration(new PhrasePart[] { new PhrasePart("List"), new PhrasePart(T) }, new ProductType(new[] { new PhrasePart(new ParameterDeclaration("obj", GenericArgumentReferenceType.For(T))) }, new[] { T }, Enumerable.Empty<Field>()));
            var listListInt = BoundGenericProductType.For((ProductType)genericList.Returns, new[] { BoundGenericProductType.For((ProductType)genericList.Returns, new[] { TangentType.Int }) });
            var listListInferT = BoundGenericProductType.For((ProductType)genericList.Returns, new[] { BoundGenericProductType.For((ProductType)genericList.Returns, new[] { inferencePlaceholder }) });
            var results = new Dictionary<ParameterDeclaration, TangentType>();

            Assert.IsTrue(listListInferT.CompatibilityMatches(listListInt, results));
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(TangentType.Int, results[genericParam]);
        }

        [TestMethod]
        public void LazyBasicPath()
        {
            var genericParam = new ParameterDeclaration("T", TangentType.Any);
            var inferencePlaceholder = GenericInferencePlaceholder.For(genericParam);
            var results = new Dictionary<ParameterDeclaration, TangentType>();

            Assert.IsTrue(inferencePlaceholder.Lazy.CompatibilityMatches(TangentType.Double.Lazy, results));
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(TangentType.Double, results[genericParam]);
        }

        [TestMethod]
        public void LazyDictionaryPath()
        {
            // Map<(K:Any), (V:Any)> vs Map<int, string> infers both.
            var genericParam1 = new ParameterDeclaration("K", TangentType.Any);
            var genericParam2 = new ParameterDeclaration("V", TangentType.Any);
            var inferencePlaceholder1 = GenericInferencePlaceholder.For(genericParam1);
            var inferencePlaceholder2 = GenericInferencePlaceholder.For(genericParam2);
            var K = new ParameterDeclaration("typeK", TangentType.Any.Kind);
            var V = new ParameterDeclaration("typeV", TangentType.Any.Kind);
            var genericMap = new TypeDeclaration(new PhrasePart[] { new PhrasePart("Map"), new PhrasePart(K), new PhrasePart(V) }, new ProductType(new[] { new PhrasePart(new ParameterDeclaration("key", GenericArgumentReferenceType.For(K))), new PhrasePart(new ParameterDeclaration("value", GenericArgumentReferenceType.For(V))) }, new[] { K, V }, Enumerable.Empty<Field>()));
            var mapIntString = BoundGenericProductType.For((ProductType)genericMap.Returns, new[] { TangentType.Int, TangentType.String });
            var mapInfer = BoundGenericProductType.For((ProductType)genericMap.Returns, new[] { inferencePlaceholder1, inferencePlaceholder2 });
            var results = new Dictionary<ParameterDeclaration, TangentType>();

            Assert.IsTrue(mapInfer.Lazy.CompatibilityMatches(mapIntString.Lazy, results));
            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(TangentType.Int, results[genericParam1]);
            Assert.AreEqual(TangentType.String, results[genericParam2]);
        }

        [TestMethod]
        public void KindBasicPath()
        {
            var genericParam = new ParameterDeclaration("T", TangentType.Any);
            var inferencePlaceholder = GenericInferencePlaceholder.For(genericParam);
            var results = new Dictionary<ParameterDeclaration, TangentType>();

            Assert.IsTrue(inferencePlaceholder.Kind.CompatibilityMatches(TangentType.Double.Kind, results));
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(TangentType.Double, results[genericParam]);
        }

        [TestMethod]
        public void ReturnTypeIsResolvedProperlyWhenBasedOnInferrence()
        {
            var genericParam = new ParameterDeclaration("T", TangentType.Any.Kind);
            var inferencePlaceholder = GenericInferencePlaceholder.For(genericParam);
            var genericReference = GenericArgumentReferenceType.For(genericParam);
            var identityFn = new ReductionDeclaration(new[] { new PhrasePart(new ParameterDeclaration("input", inferencePlaceholder)) }, new Function(genericReference, null), new[] { genericParam });

            var binding = new FunctionInvocationExpression(identityFn, new Expression[] { new ConstantExpression<int>(TangentType.Int, 42, new LineColumnRange("test", "test", 0, 0)) }, new[] { TangentType.Int }, new LineColumnRange("test", "test", 0, 0));
            Assert.AreEqual(TangentType.Int, binding.EffectiveType);
        }
    }
}
