using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using AnimeStudio.App;

namespace AnimeStudio.GUI
{
    using Color = System.Drawing.Color;

    partial class MainForm
    {
        // TreeNodes are created when their parent is expanded, the hierarchy itself lives in the SceneGraph.
        private readonly Dictionary<int, TreeNode> treeNodes = new();
        private List<int> treeSearchResults = new();
        private int nextTreeResult;
        private bool syncingChecks;

        private SceneGraph Scene => Catalog.Scene;

        private void ResetScene()
        {
            sceneTreeView.Nodes.Clear();
            treeNodes.Clear();
            treeSearchResults.Clear();
            nextTreeResult = 0;
        }

        private void PopulateScene()
        {
            sceneTreeView.BeginUpdate();
            foreach (var root in Scene.Roots)
                sceneTreeView.Nodes.Add(CreateTreeNode(root));
            sceneTreeView.EndUpdate();
        }

        private TreeNode CreateTreeNode(int id)
        {
            var node = new TreeNode(Scene.GetName(id)) { Tag = id, Checked = Scene.IsChecked(id) };
            if (Scene.HasModel(id))
                node.BackColor = Color.LightBlue;
            if (Scene.GetChildCount(id) > 0)
                node.Nodes.Add(new TreeNode());
            treeNodes[id] = node;
            return node;
        }

        private static bool HasPlaceholder(TreeNode node) => node.Nodes.Count == 1 && node.Nodes[0].Tag == null;

        private void PopulateChildren(TreeNode node)
        {
            if (!HasPlaceholder(node))
                return;
            sceneTreeView.BeginUpdate();
            node.Nodes.Clear();
            foreach (var child in Scene.GetChildren((int)node.Tag))
                node.Nodes.Add(CreateTreeNode(child));
            sceneTreeView.EndUpdate();
        }

        private void sceneTreeView_BeforeExpand(object sender, TreeViewCancelEventArgs e) => PopulateChildren(e.Node);

        private TreeNode EnsureTreeNode(int id)
        {
            if (treeNodes.TryGetValue(id, out var existing))
                return existing;

            var path = new Stack<int>();
            for (var p = Scene.GetParent(id); p != -1; p = Scene.GetParent(p))
                path.Push(p);
            while (path.Count > 0)
            {
                if (treeNodes.TryGetValue(path.Pop(), out var ancestor))
                    PopulateChildren(ancestor);
            }
            return treeNodes.GetValueOrDefault(id);
        }

        private void sceneTreeView_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (syncingChecks || e.Node.Tag is not int id)
                return;
            Scene.SetChecked(id, e.Node.Checked);
            syncingChecks = true;
            try
            {
                SyncCreatedChildren(e.Node, e.Node.Checked);
            }
            finally
            {
                syncingChecks = false;
            }
        }

        private static void SyncCreatedChildren(TreeNode node, bool value)
        {
            foreach (TreeNode child in node.Nodes)
            {
                if (child.Tag == null)
                    continue;
                child.Checked = value;
                SyncCreatedChildren(child, value);
            }
        }

        private void treeSearch_TextChanged(object sender, EventArgs e)
        {
            treeSearchResults.Clear();
            nextTreeResult = 0;
        }

        // Enter: next match. Shift: all matches. Alt: act on the root of the match. Ctrl: check instead of uncheck.
        private async void treeSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter || string.IsNullOrEmpty(treeSearch.Text))
                return;
            e.SuppressKeyPress = true;
            bool shift = e.Shift, alt = e.Alt, control = e.Control;

            if (treeSearchResults.Count == 0)
            {
                Regex regex;
                try
                {
                    regex = new Regex(treeSearch.Text, RegexOptions.IgnoreCase);
                }
                catch (ArgumentException ex)
                {
                    Logger.Error("Invalid Regex.\n" + ex.Message);
                    return;
                }
                var scene = Scene;
                treeSearchResults = await Task.Run(() => scene.Search(regex));
                if (scene != Scene)
                    return;
            }
            if (treeSearchResults.Count == 0)
                return;

            if (shift)
            {
                sceneTreeView.BeginUpdate();
                foreach (var id in treeSearchResults)
                    RevealSearchResult(id, alt, control);
                sceneTreeView.EndUpdate();
                sceneTreeView.SelectedNode = EnsureTreeNode(treeSearchResults[0]);
            }
            else
            {
                if (nextTreeResult >= treeSearchResults.Count)
                    nextTreeResult = 0;
                var id = treeSearchResults[nextTreeResult++];
                RevealSearchResult(id, alt, control);
                sceneTreeView.SelectedNode = EnsureTreeNode(id);
            }
        }

        private void RevealSearchResult(int id, bool root, bool check)
        {
            var target = EnsureTreeNode(root ? Scene.GetRoot(id) : id);
            if (target == null)
                return;
            target.EnsureVisible();
            target.Checked = check;
        }
    }
}
