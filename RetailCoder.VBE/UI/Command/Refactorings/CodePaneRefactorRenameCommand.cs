using System.Runtime.InteropServices;
using Rubberduck.Parsing.Symbols;
using Rubberduck.Parsing.VBA;
using Rubberduck.Refactorings.Rename;
using Rubberduck.Settings;
using Rubberduck.UI.Refactorings;
using Rubberduck.VBEditor.DisposableWrappers;

namespace Rubberduck.UI.Command.Refactorings
{
    [ComVisible(false)]
    public class CodePaneRefactorRenameCommand : RefactorCommandBase
    {
        private readonly RubberduckParserState _state;

        public CodePaneRefactorRenameCommand(VBE vbe, RubberduckParserState state) 
            : base (vbe)
        {
            _state = state;
        }

        public override RubberduckHotkey Hotkey
        {
            get { return RubberduckHotkey.RefactorRename; }
        }

        protected override bool CanExecuteImpl(object parameter)
        {
            if (Vbe.ActiveCodePane == null)
            {
                return false;
            }

            var target = _state.FindSelectedDeclaration(Vbe.ActiveCodePane);
            return _state.Status == ParserState.Ready && target != null && !target.IsBuiltIn;
        }

        protected override void ExecuteImpl(object parameter)
        {
            if (Vbe.ActiveCodePane == null) { return; }

            Declaration target;
            if (parameter != null)
            {
                target = parameter as Declaration;
            }
            else
            {
                target = _state.FindSelectedDeclaration(Vbe.ActiveCodePane);
            }

            if (target == null)
            {
                return;
            }

            using (var view = new RenameDialog())
            {
                var factory = new RenamePresenterFactory(Vbe, view, _state, new MessageBox());
                var refactoring = new RenameRefactoring(Vbe, factory, new MessageBox(), _state);

                refactoring.Refactor(target);
            }
        }
    }
}
