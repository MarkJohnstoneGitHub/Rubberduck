﻿using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Moq;
using NUnit.Framework;
using Rubberduck.CodeAnalysis.Inspections.Concrete;
using Rubberduck.Parsing.Grammar;
using Rubberduck.Parsing.Inspections.Abstract;
using Rubberduck.Parsing.TypeResolvers;
using Rubberduck.Parsing.VBA;
using Rubberduck.VBEditor;
using Rubberduck.VBEditor.SafeComWrappers;
using Rubberduck.VBEditor.SafeComWrappers.Abstract;
using RubberduckTests.Mocks;

namespace RubberduckTests.Inspections
{
    [TestFixture]
    public class ArgumentWithIncompatibleObjectTypeInspectionTests
    {
        [Test]
        [Category("Inspections")]
        [TestCase("Class1", "TestProject.Class1", 0)]
        [TestCase("Interface1", "TestProject.Class1", 0)]
        [TestCase("Class1", "TestProject.Interface1", 0)]
        [TestCase("Variant", "Whatever", 0)] //Tokens.Variant cannot be used here because it is not a constant expression.
        [TestCase("Object", "Whatever", 0)]
        [TestCase("Whatever", "Variant", 0)]
        [TestCase("Whatever", "Object", 0)]
        [TestCase("Class1", "TestProject.SomethingIncompatible", 1)]
        [TestCase("Class1", "SomethingDifferent", 1)]
        [TestCase("TestProject.Class1", "OtherProject.Class1", 1)]
        [TestCase("TestProject.Interface1", "OtherProject.Class1", 1)]
        [TestCase("TestProject.Class1", "OtherProject.Interface1", 1)]
        [TestCase("Class1", "OtherProject.Class1", 1)]
        [TestCase("Interface1", "OtherProject.Class1", 1)]
        [TestCase("Class1", "OtherProject.Interface1", 1)]
        [TestCase("Class1", SetTypeResolver.NotAnObject, 1)] //The argument is not even an object. (Will show as type NotAnObject in the result.) 
        [TestCase("Class1", null, 0)] //We could not resolve the Set type, so we do not return a result. 
        public void MockedSetTypeEvaluatorTest_OneArgument(string paramTypeName, string expressionFullTypeName, int expectedResultsCount)
        {
            const string interface1 =
                @"
Private Sub Foo() 
End Sub
";
            const string class1 =
                @"Implements Interface1

Private Sub Interface1_Foo()
End Sub
";

            var module1 =
                $@"
Private Sub DoIt()
    Bar expression
End Sub

Private Sub Bar(baz As {paramTypeName})
End Sub
";

            var vbe = new MockVbeBuilder()
                .ProjectBuilder("TestProject", ProjectProtection.Unprotected)
                .AddComponent("Class1", ComponentType.ClassModule, class1)
                .AddComponent("Interface1", ComponentType.ClassModule, interface1)
                .AddComponent("Module1", ComponentType.StandardModule, module1)
                .AddProjectToVbeBuilder()
                .Build()
                .Object;

            var setTypeResolverMock = new Mock<ISetTypeResolver>();
            setTypeResolverMock.Setup(m =>
                    m.SetTypeName(It.IsAny<VBAParser.ExpressionContext>(), It.IsAny<QualifiedModuleName>()))
                .Returns((VBAParser.ExpressionContext context, QualifiedModuleName qmn) => expressionFullTypeName);

            var inspectionResults = InspectionResults(vbe, setTypeResolverMock.Object).ToList();

            Assert.AreEqual(expectedResultsCount, inspectionResults.Count);
        }

        [Test]
        [Category("Inspections")] 
        public void ParamArray_NoResult()
        {
            const string interface1 =
                @"
Private Sub Foo() 
End Sub
";
            const string class1 =
                @"Implements Interface1

Private Sub Interface1_Foo()
End Sub
";

            const string module1 =
                @"
Private Sub DoIt()
    Bar New Class1, New Class1, 42, 77, 22-3
End Sub

Private Sub Bar(ParamArray baz)
End Sub
";

            var vbe = new MockVbeBuilder()
                .ProjectBuilder("TestProject", ProjectProtection.Unprotected)
                .AddComponent("Class1", ComponentType.ClassModule, class1)
                .AddComponent("Interface1", ComponentType.ClassModule, interface1)
                .AddComponent("Module1", ComponentType.StandardModule, module1)
                .AddProjectToVbeBuilder()
                .Build()
                .Object;

            var inspectionResults = InspectionResults(vbe).ToList();

            Assert.IsFalse(inspectionResults.Any());
        }

        [Test]
        [Category("Inspections")]
        public void MissingOptionalArgument_NoResult()
        {
            const string interface1 =
                @"
Private Sub Foo() 
End Sub
";
            const string class1 =
                @"Implements Interface1

Private Sub Interface1_Foo()
End Sub
";

            const string module1 =
                @"
Private Sub DoIt()
    Bar  , Nothing
End Sub

Private Sub Bar(Optional baz As Class1 = Nothing, Optional bazBaz As Class1 = Nothing)
End Sub
";

            var vbe = new MockVbeBuilder()
                .ProjectBuilder("TestProject", ProjectProtection.Unprotected)
                .AddComponent("Class1", ComponentType.ClassModule, class1)
                .AddComponent("Interface1", ComponentType.ClassModule, interface1)
                .AddComponent("Module1", ComponentType.StandardModule, module1)
                .AddProjectToVbeBuilder()
                .Build()
                .Object;

            var inspectionResults = InspectionResults(vbe).ToList();

            Assert.IsFalse(inspectionResults.Any());
        }

        private static IEnumerable<IInspectionResult> InspectionResults(params (string moduleName, string content, ComponentType componentType)[] testModules)
        {
            var vbe = MockVbeBuilder.BuildFromModules(testModules).Object;
            return InspectionResults(vbe);
        }

        private static IEnumerable<IInspectionResult> InspectionResults(ISetTypeResolver setTypeResolver, params (string moduleName, string content, ComponentType componentType)[] testModules)
        {
            var vbe = MockVbeBuilder.BuildFromModules(testModules).Object;
            return InspectionResults(vbe, setTypeResolver);
        }

        private static IEnumerable<IInspectionResult> InspectionResults(IVBE vbe, ISetTypeResolver setTypeResolver = null)
        {
            using (var state = MockParser.CreateAndParse(vbe))
            {
                var inspection = InspectionUnderTest(state, setTypeResolver);
                return inspection.GetInspectionResults(CancellationToken.None);
            }
        }

        private static IInspection InspectionUnderTest(RubberduckParserState state, ISetTypeResolver setTypeResolver = null)
        {
            var setTypeResolverToUse = setTypeResolver ?? new SetTypeResolver(state);
            return new ArgumentWithIncompatibleObjectTypeInspection(state, setTypeResolverToUse);
        }
    }
}