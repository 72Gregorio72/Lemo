using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ColorPickerControll : MonoBehaviour
{
    public float currentHue;
    public float currentSat;
    public float currentVal;

    [SerializeField]
    private RawImage hueImage, satValImage, outputImage;

    [SerializeField]
    private Slider hueSlider;

    [SerializeField]
    private TMP_InputField hexInputField;

    public Texture2D hueTexture, svTexture, outputTexture;

    [SerializeField]
    MeshRenderer changeThisColour;

    public bool isPaint = false;

    // public Material materialToChange;

    public Material materialToChange2;

    private void Start()
    {
        CreateHueImage();
        CreateSVImage();
        CreateOutputImage();

        UpdateOutputImage();
    }
    private void CreateHueImage()
    {
        int resolution = 64; // o 128 per più dettaglio

        hueTexture = new Texture2D(1, resolution)
        {
            wrapMode = TextureWrapMode.Clamp,
            name = "HueTexture"
        };

        for (int i = 0; i < resolution; i++)
        {
            float hue = (float)i / (resolution - 1); // range [0,1]
            Color color = Color.HSVToRGB(hue, 1f, 1f); // piena saturazione e luminosità
            hueTexture.SetPixel(0, i, color);
        }

        hueTexture.Apply();
        currentHue = 0f;

        hueImage.texture = hueTexture;
    }

    private void CreateSVImage()
    {
        svTexture = new Texture2D(16, 16);
        svTexture.wrapMode = TextureWrapMode.Clamp;
        svTexture.name = "SatValTexture";
        for (int x = 0; x < svTexture.width; x++)
        {
            for (int y = 0; y < svTexture.height; y++)
            {
                float s = (float)x / (svTexture.width - 1);
                float v = (float)y / (svTexture.height - 1);
                svTexture.SetPixel(x, y, Color.HSVToRGB(currentHue, s, v));
            }
        }
        svTexture.Apply();
        currentSat = 0f;
        currentVal = 0f;

        satValImage.texture = svTexture;  
    }

    private void CreateOutputImage()
    {
        outputTexture = new Texture2D(1, 16);
        outputTexture.wrapMode = TextureWrapMode.Clamp;
        outputTexture.name = "OutputTexture";
        Color currentColor;
        if (!isPaint)
            currentColor = Color.HSVToRGB(currentHue, currentSat, currentVal);
        else
            currentColor = Color.HSVToRGB(currentHue, 1f, 1f); // piena saturazione e luminosità per la vernice
        // Color currentColor = Color.red;

        for (int i = 0; i < outputTexture.height; i++)
        {
            outputTexture.SetPixel(0, i, currentColor);
        }
        outputTexture.Apply();
        outputImage.texture = outputTexture;
    }

    private void UpdateOutputImage()
    {
        Color currentColor;
        if (!isPaint)
            currentColor = Color.HSVToRGB(currentHue, currentSat, currentVal);
        else
            currentColor = Color.HSVToRGB(currentHue, 1f, 1f); // piena saturazione e luminosità per la vernice
        //Color currentColor = Color.red;
            for (int i = 0; i < outputTexture.height; i++)
            {
                outputTexture.SetPixel(0, i, currentColor);
            }
        outputTexture.Apply();
        changeThisColour.GetComponent<MeshRenderer>().material.color = currentColor;
        materialToChange2.color = currentColor;
    }

    public void SetSV(float S, float V)
    {
        currentSat = S;
        currentVal = V;

        UpdateOutputImage();
    }

    public void UpdateSVImage()
    {
        currentHue = hueSlider.value;
        for (int x = 0; x < svTexture.width; x++)
        {
            for (int y = 0; y < svTexture.height; y++)
            {
                float s = (float)x / (svTexture.width - 1);
                float v = (float)y / (svTexture.height - 1);
                svTexture.SetPixel(x, y, Color.HSVToRGB(currentHue, s, v));
            }
        }
        svTexture.Apply();
        UpdateOutputImage();
    }
}
