using UnityEngine;
using TMPro;
using System.Collections;
using Photon.Pun;

// Attached to CardPrefab: handles animation and click for bonk.
public class CardScript : MonoBehaviour
{
    public string clusterId;
    private Vector3 centerPos = Vector3.zero; // Reference to central area

    public void FloatToCenter()
    {
        // Start at random position
        transform.position = Random.insideUnitSphere * 10 + new Vector3(0, 5, 0);
        transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
        StartCoroutine(LerpToCenter());
    }

    private IEnumerator LerpToCenter()
    {
        float duration = 2f;
        Vector3 startPos = transform.position;
        // Target with small random offset to avoid overlap
        Vector3 targetPos = centerPos + Random.insideUnitSphere * 0.5f;
        float time = 0;
        while (time < duration)
        {
            transform.position = Vector3.Lerp(startPos, targetPos, time / duration);
            time += Time.deltaTime;
            yield return null;
        }
    }

    private void OnMouseDown()
    {
        // Register bonk by RPC to master
        GameManager.Instance.photonView.RPC("BonkCard", PhotonTargets.MasterClient, clusterId);
    }
}