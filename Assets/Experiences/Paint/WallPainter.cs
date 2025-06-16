using UnityEngine;

public class WallPainter : MonoBehaviour
{
    public Texture2D drawTexture;
    public Color drawColor = Color.black;
    public int brushSize = 4;

    private Renderer rend;

    private void Start()
    {
        rend = GetComponent<Renderer>();

        drawTexture = new Texture2D(1024, 1024, TextureFormat.RGBA32, false);
        drawTexture.filterMode = FilterMode.Point;

        // Inizializza la texture con colore bianco
        Color[] fillColorArray = drawTexture.GetPixels();
        for (int i = 0; i < fillColorArray.Length; ++i)
        {
            fillColorArray[i] = Color.white;
        }
        drawTexture.SetPixels(fillColorArray);
        drawTexture.Apply();

        // Assegna la texture al materiale
        rend.material.mainTexture = drawTexture;
    }

    public void DrawAt(Vector2 uv)
    {
        Debug.Log($"Drawing at UV: {uv}");
        int x = (int)(uv.x * drawTexture.width);
        int y = (int)(uv.y * drawTexture.height);

        for (int i = -brushSize; i <= brushSize; i++)
        {
            for (int j = -brushSize; j <= brushSize; j++)
            {
                int drawX = Mathf.Clamp(x + i, 0, drawTexture.width - 1);
                int drawY = Mathf.Clamp(y + j, 0, drawTexture.height - 1);
                drawTexture.SetPixel(drawX, drawY, drawColor);
            }
        }

        drawTexture.Apply();
    }
}
