/* Flashing button example */
using System;
using UnityEngine;

public class GUITest : MonoBehaviour
{
    [SerializeField] private GUIStyle guiStyle;
    [SerializeField] private GUIStyle guiStyleForEndgame;
    [SerializeField] private GUIStyle dodgeBarBackground;
    [SerializeField] private GUIStyle dodgeBar;

    static public string instancesCounts = "";
    static public string centerScreenText = "";
    static public string bottomLeftText = "";
    [Range(0, 1)] static public float dodgeCooldown = 0f;

    void OnGUI () 
    {
        GUI.Label(new Rect(10, 10, 100, 100), instancesCounts, guiStyle);

        GUI.Label(new Rect(Screen.width / 2 - 100, Screen.height / 2 - 100, 200, 200), centerScreenText, guiStyleForEndgame);

        GUI.Label(new Rect(10, Screen.height - 50, 100, 100), bottomLeftText, guiStyle);

        if (dodgeCooldown > 0) {
            // Draw dodge bar background, and the dodge bar itself
            // This bar is drawn in the bottom center of the screen
            var barWidth = 300;
            var barHeight = 20;
            var yOffset = 100f;
            GUI.Box(new Rect(Screen.width / 2 - barWidth / 2, Screen.height - barHeight - yOffset, barWidth, barHeight), "", dodgeBarBackground);
            GUI.Box(new Rect(Screen.width / 2 - barWidth / 2, Screen.height - barHeight - yOffset, barWidth * dodgeCooldown, barHeight), "", dodgeBar);
        }

        // if (Time.time % 2 < 1) 
        // {
        //     if (GUI.Button (new Rect (10,10,200,20), "Meet the flashing button"))
        //     {
        //         print ("You clicked me!");
        //     }
        // }
    }

    void Update()
    {
        instancesCounts = "";
        centerScreenText = "";
        bottomLeftText = "";
    }

    public static string Prettify(int n)
    {
        if (n < 1000) return n.ToString();
        int exp = (int)(Mathf.Log(n) / Mathf.Log(1000));
        return string.Format("{0:0.##} {1}", n / Mathf.Pow(1000, exp), "kMGTPE"[exp - 1]);
    }
}
