using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Follow : MonoBehaviour
{
    public GameObject target;
    public float max_speed = 30f;
    public float max_distence = 5f;

    // Update is called once per frame
    void Update()
    {
        if(target == null) {  return; }
        Vector3 delta_pos = target.transform.position - transform.position;
        if (delta_pos.magnitude > max_distence + max_speed * Time.deltaTime)
        {
            delta_pos = delta_pos.normalized * max_distence;
            transform.position = target.transform.position - delta_pos;
        } else if (delta_pos.magnitude > max_speed * Time.deltaTime)
        {
            delta_pos = delta_pos.normalized * max_speed * Time.deltaTime;
            transform.position = transform.position + delta_pos;
        } else
        {
            delta_pos *= delta_pos.magnitude / (max_speed * Time.deltaTime);
            transform.position = transform.position + delta_pos;
        }
        delta_pos = target.transform.position - transform.position;

        Quaternion target_rot = target.transform.rotation;
        Quaternion start_rot = transform.rotation;
        float lerp = Mathf.Clamp(delta_pos.magnitude / max_distence, 0.0f, 0.9f);
        transform.rotation = Quaternion.Lerp(target_rot, start_rot, lerp);
    }
}
