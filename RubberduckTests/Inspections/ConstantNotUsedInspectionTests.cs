using System.Linq;
using System.Threading;
using NUnit.Framework;
using Rubberduck.Inspections.Concrete;
using RubberduckTests.Mocks;

namespace RubberduckTests.Inspections
{
    [TestFixture]
    public class ConstantNotUsedInspectionTests
    {
        [Test]
        [Category("Inspections")]
        public void ConstantNotUsed_ReturnsResult()
        {
            const string inputCode =
                @"Public Sub Foo()
    Const const1 As Integer = 9
End Sub";

            var vbe = MockVbeBuilder.BuildFromSingleStandardModule(inputCode, out _);
            using (var state = MockParser.CreateAndParse(vbe.Object))
            {

                var inspection = new ConstantNotUsedInspection(state);
                var inspectionResults = inspection.GetInspectionResults(CancellationToken.None);

                Assert.AreEqual(1, inspectionResults.Count());
            }
        }

        [Test]
        [Category("Inspections")]
        public void ConstantNotUsed_ReturnsResult_MultipleConsts()
        {
            const string inputCode =
                @"Public Sub Foo()
    Const const1 As Integer = 9
    Const const2 As String = ""test""
End Sub";

            var vbe = MockVbeBuilder.BuildFromSingleStandardModule(inputCode, out _);
            using (var state = MockParser.CreateAndParse(vbe.Object))
            {

                var inspection = new ConstantNotUsedInspection(state);
                var inspectionResults = inspection.GetInspectionResults(CancellationToken.None);

                Assert.AreEqual(2, inspectionResults.Count());
            }
        }

        [Test]
        [Category("Inspections")]
        public void ConstantNotUsed_ReturnsResult_UnusedConstant()
        {
            const string inputCode =
                @"Public Sub Foo()
    Const const1 As Integer = 9
    Goo const1

    Const const2 As String = ""test""
End Sub

Public Sub Goo(ByVal arg1 As Integer)
End Sub";

            var vbe = MockVbeBuilder.BuildFromSingleStandardModule(inputCode, out _);
            using (var state = MockParser.CreateAndParse(vbe.Object))
            {

                var inspection = new ConstantNotUsedInspection(state);
                var inspectionResults = inspection.GetInspectionResults(CancellationToken.None);

                Assert.AreEqual(1, inspectionResults.Count());
            }
        }

        [Test]
        [Category("Inspections")]
        public void ConstantNotUsed_DoesNotReturnResult()
        {
            const string inputCode =
                @"Public Sub Foo()
    Const const1 As Integer = 9
    Goo const1
End Sub

Public Sub Goo(ByVal arg1 As Integer)
End Sub";

            var vbe = MockVbeBuilder.BuildFromSingleStandardModule(inputCode, out _);
            using (var state = MockParser.CreateAndParse(vbe.Object))
            {

                var inspection = new ConstantNotUsedInspection(state);
                var inspectionResults = inspection.GetInspectionResults(CancellationToken.None);

                Assert.AreEqual(0, inspectionResults.Count());
            }
        }

        [Test]
        [Category("Inspections")]
        //See issue #4994 at https://github.com/rubberduck-vba/Rubberduck/issues/4994
        public void ConstantNotUsed_ConstantOnlyUsedInMidStatement_DoesNotReturnResult()
        {
            const string inputCode =
                @"Function UnAccent(ByVal inputString As String) As String
          Dim Index As Long, Position As Long
         Const ACCENTED_CHARS As String = ""�������������������������������������������������������������؟��""
         Const ANSICHARACTERS As String = ""SZszYAAAAAACEEEEIIIIDNOOOOOUUUUYaaaaaaceeeeiiiidnooooouuuuyyoOYoO""
   For Index = 1 To Len(inputString)
     Position = InStr(ACCENTED_CHARS, Mid$(inputString, Index, 1))
     If Position Then Mid$(inputString, Index) = Mid$(ANSICHARACTERS, Position, 1)
    Next
     UnAccent = inputString
End Function";

            var vbe = MockVbeBuilder.BuildFromSingleStandardModule(inputCode, out _);
            using (var state = MockParser.CreateAndParse(vbe.Object))
            {

                var inspection = new ConstantNotUsedInspection(state);
                var inspectionResults = inspection.GetInspectionResults(CancellationToken.None);

                Assert.AreEqual(0, inspectionResults.Count());
            }
        }

        [Test]
        [Category("Inspections")]
        public void ConstantNotUsed_IgnoreModule_All_YieldsNoResult()
        {
            const string inputCode =
                @"'@IgnoreModule

Public Sub Foo()
    Const const1 As Integer = 9
End Sub";

            var vbe = MockVbeBuilder.BuildFromSingleStandardModule(inputCode, out _);
            using (var state = MockParser.CreateAndParse(vbe.Object))
            {

                var inspection = new ConstantNotUsedInspection(state);
                var inspectionResults = inspection.GetInspectionResults(CancellationToken.None);

                Assert.IsFalse(inspectionResults.Any());
            }
        }

        [Test]
        [Category("Inspections")]
        public void ConstantNotUsed_IgnoreModule_AnnotationName_YieldsNoResult()
        {
            const string inputCode =
                @"'@IgnoreModule ConstantNotUsed

Public Sub Foo()
    Const const1 As Integer = 9
End Sub";

            var vbe = MockVbeBuilder.BuildFromSingleStandardModule(inputCode, out _);
            using (var state = MockParser.CreateAndParse(vbe.Object))
            {

                var inspection = new ConstantNotUsedInspection(state);
                var inspectionResults = inspection.GetInspectionResults(CancellationToken.None);

                Assert.IsFalse(inspectionResults.Any());
            }
        }

        [Test]
        [Category("Inspections")]
        public void ConstantNotUsed_IgnoreModule_OtherAnnotationName_YieldsResults()
        {
            const string inputCode =
                @"'@IgnoreModule VariableNotUsed

Public Sub Foo()
    Const const1 As Integer = 9
End Sub";

            var vbe = MockVbeBuilder.BuildFromSingleStandardModule(inputCode, out _);
            using (var state = MockParser.CreateAndParse(vbe.Object))
            {

                var inspection = new ConstantNotUsedInspection(state);
                var inspectionResults = inspection.GetInspectionResults(CancellationToken.None);

                Assert.IsTrue(inspectionResults.Any());
            }
        }

        [Test]
        [Category("Inspections")]
        public void ConstantNotUsed_Ignored_DoesNotReturnResult()
        {
            const string inputCode =
                @"Public Sub Foo()
    '@Ignore ConstantNotUsed
    Const const1 As Integer = 9
End Sub";

            var vbe = MockVbeBuilder.BuildFromSingleStandardModule(inputCode, out _);
            using (var state = MockParser.CreateAndParse(vbe.Object))
            {

                var inspection = new ConstantNotUsedInspection(state);
                var inspectionResults = inspection.GetInspectionResults(CancellationToken.None);

                Assert.IsFalse(inspectionResults.Any());
            }
        }

        [Test]
        [Category("Inspections")]
        public void InspectionName()
        {
            const string inspectionName = "ConstantNotUsedInspection";
            var inspection = new ConstantNotUsedInspection(null);

            Assert.AreEqual(inspectionName, inspection.Name);
        }
    }
}
