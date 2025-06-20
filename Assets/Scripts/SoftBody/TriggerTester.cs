using UnityEngine;

public class TriggerTester : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        Debug.Log("TRIGGER ENTER: " + other.name);
    }

    void OnTriggerStay(Collider other)
    {
        Debug.Log("TRIGGER STAY: " + other.name);
    }

    void OnTriggerExit(Collider other)
    {
        Debug.Log("TRIGGER EXIT: " + other.name);
    }
}
