﻿using System.Collections.Concurrent;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Vbe.Interop;
using Rubberduck.Extensions;
using Rubberduck.VBA;
using Rubberduck.VBA.Nodes;

namespace Rubberduck.UI.CodeExplorer
{
    [ComVisible(false)]
    public class CodeExplorerDockablePresenter : DockablePresenterBase
    {
        private readonly IRubberduckParser _parser;
        private CodeExplorerWindow Control { get { return UserControl as CodeExplorerWindow; } }

        public CodeExplorerDockablePresenter(IRubberduckParser parser, VBE vbe, AddIn addIn)
            : base(vbe, addIn, new CodeExplorerWindow())
        {
            _parser = parser;
            Control.SolutionTree.Font = new Font(Control.SolutionTree.Font, FontStyle.Bold);
            RegisterControlEvents();
            RefreshExplorerTreeView();
            Control.SolutionTree.Refresh();
        }

        private void RegisterControlEvents()
        {
            if (Control == null)
            {
                return;
            }

            Control.RefreshTreeView += RefreshExplorerTreeView;
            Control.NavigateTreeNode += NavigateExplorerTreeNode;
            Control.SolutionTree.AfterExpand += TreeViewAfterExpandNode;
            Control.SolutionTree.AfterCollapse += TreeViewAfterCollapseNode;
        }

        private void NavigateExplorerTreeNode(object sender, CodeExplorerNavigateArgs e)
        {
            if (!e.Node.IsExpanded)
            {
                e.Node.Expand();
            }

            var projectName = e.QualifiedName.ProjectName;
            var componentName = e.QualifiedName.ModuleName;

            var project = VBE.VBProjects.Cast<VBProject>()
                               .FirstOrDefault(p => p.Name == projectName);

            VBComponent component = null;
            if (project != null)
            {
                component = project.VBComponents.Cast<VBComponent>()
                                       .FirstOrDefault(c => c.Name == componentName);
            }

            if (component == null)
            {
                return;
            }

            var codePane = component.CodeModule.CodePane;

            if (e.Selection.StartLine != 0)
            {
                //hack: get around issue where a node's selection seems to ignore a procedure's (or enum's) signature
                var selection = new Selection(e.Selection.StartLine,
                                                1,
                                                e.Selection.EndLine,
                                                e.Selection.EndColumn == 1 ? 0 : e.Selection.EndColumn //fixes off by one error when navigating the module
                                              );
                codePane.SetSelection(selection);
                //codePane.SetSelection(e.Selection);
            }
        }

        private void RefreshExplorerTreeView()
        {
            Control.SolutionTree.Nodes.Clear();
            var projects = VBE.VBProjects.Cast<VBProject>().OrderBy(project => project.Name);
            foreach (var vbProject in projects)
            {
                AddProjectNode(vbProject);
            }
        }

        private void RefreshExplorerTreeView(object sender, System.EventArgs e)
        {
            RefreshExplorerTreeView();
        }

        private void AddProjectNode(VBProject project)
        {
            var treeView = Control.SolutionTree;

            var projectNode = new TreeNode();
            projectNode.Text = project.Name;
            projectNode.Tag = new QualifiedSelection();

            projectNode.ImageKey = "ClosedFolder";
            treeView.BackColor = treeView.BackColor;

            var moduleNodes = CreateModuleNodes(project, new Font(treeView.Font, FontStyle.Regular));

            projectNode.Nodes.AddRange(moduleNodes.ToArray());
            treeView.Nodes.Add(projectNode);
        }

        private ConcurrentBag<TreeNode> CreateModuleNodes(VBProject project, Font font)
        {
            var moduleNodes = new ConcurrentBag<TreeNode>();

            foreach (VBComponent component in project.VBComponents)
            {
                var moduleNode = new TreeNode(component.Name);
                moduleNode.NodeFont = font;
                moduleNode.ImageKey = GetComponentImageKey(component.Type);
                moduleNode.SelectedImageKey = moduleNode.ImageKey;

                var qualifiedModuleName = new Inspections.QualifiedModuleName(project.Name, component.Name);
                moduleNode.Tag = new QualifiedSelection(qualifiedModuleName, Selection.Empty);

                var parserNode = _parser.Parse(project.Name, component.Name, component.CodeModule.Lines());

                AddNodes<OptionNode>(moduleNode, parserNode, qualifiedModuleName, CreateOptionNode);
                AddNodes<EnumNode>(moduleNode, parserNode, qualifiedModuleName, CreateEnumNode);
                AddNodes<TypeNode>(moduleNode, parserNode, qualifiedModuleName, CreateTypeNode);
                AddNodes<ConstDeclarationNode>(moduleNode, parserNode, qualifiedModuleName, CreateDeclaredIdentifierNode);
                AddNodes<VariableDeclarationNode>(moduleNode, parserNode, qualifiedModuleName, CreateDeclaredIdentifierNode);
                AddNodes<ProcedureNode>(moduleNode, parserNode, qualifiedModuleName, CreateProcedureNode);

                moduleNodes.Add(moduleNode);
            }
            return moduleNodes;
        }

        private delegate TreeNode CreateTreeNode(INode node);
        private void AddNodes<T>(TreeNode parentNode, INode parserNode, Inspections.QualifiedModuleName qualifiedModuleName, CreateTreeNode createTreeNodeDelegate)
        {
            foreach (INode node in parserNode.Children.OfType<T>())
            {
                var treeNode = createTreeNodeDelegate(node);
                treeNode.Tag = new QualifiedSelection(qualifiedModuleName, node.Selection);
                parentNode.Nodes.Add(treeNode);
            }
        }

        private TreeNode CreateEnumNode(INode node)
        {
            var enumNode = (EnumNode)node;
            var result = new TreeNode(enumNode.Identifier.Name);
            result.ImageKey = enumNode.Accessibility.ToString() + "Enum";
            result.SelectedImageKey = result.ImageKey;

            var qualifiedModuleName = SplitScope(node.ParentScope);

            foreach (EnumConstNode child in node.Children)
            {
                var childNode = new TreeNode(child.IdentifierName);
                childNode.ImageKey = "EnumItem";
                childNode.SelectedImageKey = childNode.ImageKey;
                childNode.Tag = new QualifiedSelection(qualifiedModuleName, child.Selection);

                result.Nodes.Add(childNode);
            }

            return result;
        }

        private TreeNode CreateTypeNode(INode node)
        {
            var typeNode = (TypeNode)node;
            var result = new TreeNode(typeNode.Identifier.Name);
            result.ImageKey = typeNode.Accessibility.ToString() + "Type";
            result.SelectedImageKey = result.ImageKey;

            var qualifiedModuleName = SplitScope(node.ParentScope);

            foreach (TypeElementNode child in node.Children)
            {
                var childNode = new TreeNode(child.IdentifierName);
                childNode.ImageKey = "PublicField";
                childNode.SelectedImageKey = childNode.ImageKey;
                childNode.Tag = new QualifiedSelection(qualifiedModuleName, child.Selection);

                result.Nodes.Add(childNode);
            }

            return result;
        }

        private Inspections.QualifiedModuleName SplitScope(string scope)
        {
            //Assumes the scope will be in the form "Project.Module". This isn't very robust.
            var arr = scope.Split(new char[] { '.' });
            return new Inspections.QualifiedModuleName(arr[0], arr[1]);
        }

        private TreeNode CreateOptionNode(INode node)
        {
            var optionNode = (OptionNode)node;
            var treeNode = new TreeNode("Option" + optionNode.Option);
            treeNode.ImageKey = "Option";
            treeNode.SelectedImageKey = treeNode.ImageKey;

            return treeNode;
        }

        private TreeNode CreateDeclaredIdentifierNode(INode node)
        {
            var identifierNode = (DeclaredIdentifierNode)node.Children.First(); //a constant declaration node will only ever have a single child (I think)
            var treeNode = new TreeNode(identifierNode.Name);
            if (node is ConstDeclarationNode)
            {
                treeNode.ImageKey = identifierNode.Accessibility + "Const";
            }
            if (node is VariableDeclarationNode)
            {
                treeNode.ImageKey = identifierNode.Accessibility + "Field";
            }

            treeNode.SelectedImageKey = treeNode.ImageKey;
            return treeNode;
        }

        private TreeNode CreateProcedureNode(INode node)
        {
            var result = new TreeNode(node.LocalScope);
            result.ImageKey = GetProcedureImageKey((ProcedureNode)node);
            result.SelectedImageKey = result.ImageKey;
            return result;
        }

        private string GetProcedureImageKey(ProcedureNode node)
        {
            string procKind = string.Empty; //initialize to empty to shut the compiler up
            switch (node.Kind)
            {
                case ProcedureNode.VBProcedureKind.Sub:
                case ProcedureNode.VBProcedureKind.Function:
                    procKind = "Method";
                    break;
                case ProcedureNode.VBProcedureKind.PropertyGet:
                case ProcedureNode.VBProcedureKind.PropertyLet:
                case ProcedureNode.VBProcedureKind.PropertySet:
                    procKind = "Property";
                    break;
            }

            return node.Accessibility.ToString() + procKind;
        }

        private string GetComponentImageKey(vbext_ComponentType componentType)
        {
            //todo: figure out how to get to Interfaces; ImageKey = "PublicInterface"
            switch (componentType)
            {
                case vbext_ComponentType.vbext_ct_ClassModule:
                case vbext_ComponentType.vbext_ct_Document:
                case vbext_ComponentType.vbext_ct_MSForm:
                    return "ClassModule";
                case vbext_ComponentType.vbext_ct_StdModule:
                    return "StandardModule";
                case vbext_ComponentType.vbext_ct_ActiveXDesigner:
                default:
                    return string.Empty;
            }
        }

        private void TreeViewAfterExpandNode(object sender, TreeViewEventArgs e)
        {
            if (!e.Node.ImageKey.Contains("Folder"))
            {
                return;
            }

            e.Node.ImageKey = "OpenFolder";
            e.Node.SelectedImageKey = e.Node.ImageKey;
        }

        private void TreeViewAfterCollapseNode(object sender, TreeViewEventArgs e)
        {
            if (!e.Node.ImageKey.Contains("Folder"))
            {
                return;
            }

            e.Node.ImageKey = "ClosedFolder";
            e.Node.SelectedImageKey = e.Node.ImageKey;
        }
    }
}
