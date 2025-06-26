using UnityEngine;

public class BVHModel : MonoBehaviour
{
    public GameObject targetGameObject;
    public bool isOn = false;
    private bool i = true;
    private void Update()
    {
        if (isOn != i)
        {
            ToggleBVHObjects(isOn);
            i = isOn;
        }
    }
    public void ToggleBVHObjects(bool isON)
    {
        if (targetGameObject == null)
        {
            Debug.LogWarning("Target GameObject is not assigned.");
            return;
        }

        BVHObject[] bvhObjects = targetGameObject.GetComponentsInChildren<BVHObject>();
        MassSpringSystem[] MMSs = targetGameObject.GetComponentsInChildren<MassSpringSystem>();
        MeshRenderer[] meshRenderers = targetGameObject.GetComponentsInChildren<MeshRenderer>();

        foreach (MeshRenderer meshRenderer in meshRenderers)
        {
            meshRenderer.enabled = isON;
        }
        foreach (BVHObject bvhObject in bvhObjects)
        {
            bvhObject.isON = isON;
        }
        foreach (MassSpringSystem MMS in MMSs)
        {
            MMS.isDeforming = isON;
        }
        Debug.Log("All BVHObjects have been updated.");
    }
}
