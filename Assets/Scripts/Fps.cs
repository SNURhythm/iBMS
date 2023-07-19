using UnityEngine;

public class Fps : MonoBehaviour
{
    [Range(10, 150)] public int fontSize = 30;

    private void OnGUI()
    {
        var position = new Rect(0.2f, 0.2f, Screen.width, Screen.height);

        var fps = 1.0f / Time.deltaTime;
        var ms = Time.deltaTime * 1000.0f;
        var text = $"{fps:N1} FPS ({ms:N1}ms)";

        var style = new GUIStyle
        {
            fontSize = fontSize,
            normal =
            {
                textColor = Color.white
            }
        };

        GUI.Label(position, text, style);
    }
}