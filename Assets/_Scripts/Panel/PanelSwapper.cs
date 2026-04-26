using System.Collections.Generic;
using UnityEngine;

namespace SteamLobbyTutorial
{
    public class PanelSwapper : MonoBehaviour
    {
        public List<LobbyPanel> panels = new List<LobbyPanel>();

        public void SwapPanel(string panelName)
        {
            foreach (LobbyPanel panel in panels)
            {
                if (panel.PanelName == panelName)
                {
                    panel.gameObject.SetActive(true);
                }
                else
                {
                    panel.gameObject.SetActive(false);
                }
            }
        }
    }
}