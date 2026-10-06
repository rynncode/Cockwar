using UnityEngine;

/// <summary>
/// The "+1 GRENADE" text that floats up when a supply crate is opened, in the pixel font and
/// in the colour of the player who opened it. Removes itself when it has faded out.
/// </summary>
public class CratePickupText : MonoBehaviour
{
    private const float Seconds = 1.8f;
    private const float Rise = 6f;

    private PixelNumber text;
    private Vector3 start;
    private float age;

    public static void Show(string message, Vector3 position, float pixelSize, CockroachShooting collector)
    {
        GameObject holder = new GameObject("Crate Pickup Text");
        CratePickupText popup = holder.AddComponent<CratePickupText>();

        PlayerLabel label = collector != null ? collector.GetComponent<PlayerLabel>() : null;
        Color color = label != null ? PlayerLabel.GetPlayerColor(label.playerNumber) : new Color(1f, 0.9f, 0.3f, 1f);

        popup.start = position;
        popup.text = new PixelNumber(holder.transform, "Text", 0, 300, pixelSize);
        popup.text.SetColor(color);
        popup.text.SetText(message);
        holder.transform.position = position;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = age / Seconds;

        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        // Quick pop up, then a slow drift; fully visible for the first 60%.
        float rise = Rise * (1f - (1f - t) * (1f - t));
        transform.position = start + Vector3.up * rise;
        text.SetAlpha(t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f);
    }

    private void OnDestroy()
    {
        text?.Destroy();
    }
}
