using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class Cutgrass : MonoBehaviour
{
    [SerializeField]
    GrassControl grassComputeScript;

    [SerializeField]
    float radius = 1f;

    public bool updateCuts;

    Vector3 cachedPos;
    // Start is called before the first frame update


    // Update is called once per frame
    void Update()
    {
        if (updateCuts && transform.position != cachedPos)
        {
            Debug.Log("Cutting");
            grassComputeScript.UpdateCutBuffer(transform.position, radius);
            cachedPos = transform.position;

        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1, 0, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}