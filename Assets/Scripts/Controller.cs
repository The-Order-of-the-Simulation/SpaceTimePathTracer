using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Controller : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
      
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.Space))
        {
            GetComponent<Simulation>().genpoints = false;
        }
        if (Input.GetKey(KeyCode.F))
        {
            GetComponent<Simulation>().genpoints = true;
        }
    }
}
