using UnityEngine;
using System.Collections;
using Photon.Pun;

/// <summary>
/// Handles card behavior: floating to center and registering bonks.
/// </summary>
public class CardScript : MonoBehaviour
{
    public string clusterId;
    private readonly Vector3 centerPos = Vector3.zero;

    public void FloatToCenter()
    {
        // Start at a random 3D position near center
        transform.position = Random.insideUnitSphere * 10f + new Vector3(0, 5, 0);
        transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        StartCoroutine(LerpToCenter());
    }

    private IEnumerator LerpToCenter()
    {
        float duration = 2f;
        Vector3 startPos = transform.position;
        Vector3 targetPos = centerPos + Random.insideUnitSphere * 0.5f;

        float t = 0;
        while (t < duration)
        {
            transform.position = Vector3.Lerp(startPos, targetPos, t / duration);
            t += Time.deltaTime;
            yield return null;
        }
        transform.position = targetPos; // ensure final placement
    }

    private void OnMouseDown()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.photonView.RPC("BonkCard", RpcTarget.MasterClient, clusterId);
    }
}
