using UnityEngine;
using UnityEngine.UI;

public class DisplayScreen : MonoBehaviour
{
    public Text intfoText;

    void Start()
    {
        MultScreen();
    }

    private void Update()
    {
       
           // MultScreen();
        
    }

    void MultScreen()
    {

       // Debug.Log(GetType() + "/MultScreen()/ Display.displays.Length = " + Display.displays.Length);
       // intfoText.text = "当前获得屏幕数量为：" + Display.displays.Length;
        for (int i = 0; i < Display.displays.Length; i++)
        {
            Display.displays[i].Activate();
            Screen.SetResolution(Display.displays[i].renderingWidth, Display.displays[i].renderingHeight, true);
            
        }
    }
}