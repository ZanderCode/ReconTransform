using Unity.Netcode;
using UnityEngine;

public class SphereMover : NetworkBehaviour
{
    public float moveSpeed;

    public void MoveTowardsTarget()
    {
        Vector3 dir = (transform.position - GameObject.Find("FollowThisObject").transform.position).normalized;
        GetComponent<ReconTransform>().AddForce(-dir * moveSpeed * Time.deltaTime,forceMode: ForceMode.Impulse);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            KnockBack();

        MoveTowardsTarget();
    }

    [Rpc(SendTo.Server)]
    public void KnockEveryoneElseRpc(Vector3 dir, RpcParams rpcParams = default)
    {
        KnockRpc(rpcParams.Receive.SenderClientId, dir);
    }

    [Rpc(SendTo.Everyone)]
    private void KnockRpc(ulong sender, Vector3 dir)
    {
        if (sender != OwnerClientId)
            GetComponent<ReconTransform>().AddForce(dir * 15, forceMode: ForceMode.Impulse);
    }

    public void KnockBack()
    {
        Vector3 dir = new Vector3(Random.Range(-1f, 1f),0,Random.Range(-1f, 1f));
        
        // Apply to self
        GetComponent<ReconTransform>().AddForce(dir * 15, forceMode: ForceMode.Impulse);

        // Apply to everyone else
        KnockEveryoneElseRpc(dir);
    }


    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "bullet")
        {
            KnockBack();
            Destroy(other.gameObject);
        }
    }
}
