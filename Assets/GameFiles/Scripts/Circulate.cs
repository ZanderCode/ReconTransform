using Unity.Netcode;
using UnityEngine;

public class Circulate : NetworkBehaviour
{
    public float size;
    public float speed;
    float time = 0;
    void Update()
    {
        if (IsHost)
        {
            time += Time.deltaTime;
            transform.position = new Vector3(Mathf.Sin(time  * speed) * size, 0, Mathf.Cos(time  * speed) * size);
        }
    }
}
