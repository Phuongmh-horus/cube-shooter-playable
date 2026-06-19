using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WallControl : MonoBehaviour
{
    [SerializeField] private GameObject wallRenderer;
    
    public void ChangeState()
    {
        wallRenderer.transform.localScale    = new Vector3(-wallRenderer.transform.localScale.x,    wallRenderer.transform.localScale.y, wallRenderer.transform.localScale.z);
        wallRenderer.transform.localPosition = new Vector3(-wallRenderer.transform.localPosition.x, wallRenderer.transform.localPosition.y,   wallRenderer.transform.localPosition.z);
    }
}
