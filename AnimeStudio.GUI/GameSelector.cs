using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AnimeStudio.GUI
{
    partial class GameSelector : Form
    {
        #region Games
        static readonly List<GameType[]> HoyoGames = new List<GameType[]>
        {
            new[] { GameType.GI, GameType.GI_Pack, GameType.GI_CB1, GameType.GI_CB2, GameType.GI_CB3, GameType.GI_CB3Pre },
            new[] { GameType.BH3, GameType.BH3Pre, GameType.BH3PrePre },
            new[] { GameType.SR, GameType.SR_CB2 },
            new[] { GameType.ZZZ, GameType.ZZZ_CB1, GameType.ZZZ_CB2 },
            new[] { GameType.HNA_CB1 },
            new[] { GameType.HYG_CB1 },
            new[] { GameType.TOT },
        };

        static readonly Game[] UnityGames = GameManager.GetGames().Where(x => x.Category == GameCategory.Unity).ToArray();

        static readonly Game[] OtherGames = GameManager.GetGames().Where(x => x.Category == GameCategory.Other).OrderBy(g => g.DisplayName).ToArray();
        #endregion

        private Game selectedGame = GameManager.GetGame(0);
        private readonly MainForm _parent;

        public GameSelector(MainForm parent)
        {
            InitializeComponent();
            _parent = parent;
        }

        private void gameTypeCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            gameCombo.Items.Clear();
            hoyoCombo.Enabled = false;
            hoyoCombo.Items.Clear();

            customKeyText.Enabled = false;
            customKeyText.Text = "";

            switch (gameTypeCombo.SelectedIndex)
            {
                case 0:
                    gameCombo.Items.AddRange(["Genshin Impact", "Honkai Impact 3rd", "Honkai: Star Rail", "Zenless Zone Zero", "Nexus Anima", "Petit Planet", "Tears of Themis"]);
                    break;
                case 1:
                    gameCombo.Items.AddRange(OtherGames.Select(g => g.DisplayName).ToArray());
                    break;
                case 2:
                    gameCombo.Items.AddRange(UnityGames.Select(x => x.DisplayName).ToArray());
                    break;
            };

            gameCombo.Enabled = true;
        }

        private void gameCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            customKeyText.Enabled = false;
            customKeyText.Text = "";

            switch (gameTypeCombo.SelectedIndex)
            {
                case 0:
                    hoyoCombo.Items.Clear();
                    hoyoCombo.Items.AddRange(HoyoGames[gameCombo.SelectedIndex].Select(x => GameManager.GetGame(x).DisplayName).ToArray());
                    hoyoCombo.SelectedIndex = 0;
                    hoyoCombo.Enabled = true;
                    break;
                case 1:
                    selectedGame = OtherGames[gameCombo.SelectedIndex];
                    break;
                case 2:
                    selectedGame = UnityGames[gameCombo.SelectedIndex];
                    if (selectedGame.Type == GameType.UnityCNCustomKey)
                    {
                        customKeyText.Enabled = true;
                        customKeyText.Text = "";
                    }
                    break;
            }
        }

        private void hoyoCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            selectedGame = GameManager.GetGame(HoyoGames[gameCombo.SelectedIndex][hoyoCombo.SelectedIndex]);
        }

        private void confirmBtn_Click(object sender, EventArgs e)
        {
            if (selectedGame.DisplayName == "UnityCN Custom Key")
            {
                if (selectedGame is UnityCNGame unityCNGame)
                {
                    unityCNGame.Key.Key = customKeyText.Text;
                    Properties.Settings.Default.lastUnityCNKey = customKeyText.Text;
                    Properties.Settings.Default.Save();
                }
            }
            _parent.updateGame(selectedGame.Type);
            this.Close();
        }
    }
}
