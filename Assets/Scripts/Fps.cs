using UnityEngine;

public class Fps : MonoBehaviour
{
    [Range(10, 150)] public int fontSize = 30;

    public Color color = new(.0f, .0f, .0f, 1.0f);
    public float width, height;

    private void OnGUI()
    {
        var position = new Rect(width, height, Screen.width, Screen.height);

        var fps = 1.0f / Time.deltaTime;
        var ms = Time.deltaTime * 1000.0f;
        var text = $"{fps:N1} FPS ({ms:N1}ms)";

        var style = new GUIStyle
        {
            fontSize = fontSize,
            normal =
            {
                textColor = color
            }
        };

        GUI.Label(position, text, style);
    }
}