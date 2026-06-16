using UnityEngine;

public class WallControl : MonoBehaviour
{
    [SerializeField] private GameObject wallRenderer;

    public void RevertPosition()
    {
        wallRenderer.transform.localPosition = new Vector3(wallRenderer.transform.localPosition.x * -1, wallRenderer.transform.localPosition.y, wallRenderer.transform.localPosition.z);
    }
}
