using TMPro;
using Unity.Netcode;
using UnityEngine;

public class PlayerManager : NetworkBehaviour
{
    private TMP_Text clientServerText;
    public GameObject bullet;
    public float bulletSpeed;

    public override void OnNetworkSpawn()
    {
        clientServerText = GameObject.Find("ClientServerText").GetComponent<TMP_Text>();
        GameObject.Find("HostButton")?.SetActive(false);
        GameObject.Find("ClientsJoin")?.SetActive(false);

        base.OnNetworkSpawn();
        if (IsServer)
        {
            clientServerText.text = "Server";
        }
        else
        {
            clientServerText.text = "Client " + ClientManager.GetClientByNetworkId(this.NetworkObjectId).OwnerClientId;
        }
    }

    private void Update()
    {
        if (IsLocalPlayer)
        {
            if (Input.GetMouseButtonDown(0))
            {
                _Shoot();
            }
        }
    }

    private void _Shoot()
    {
        Ray r = Camera.main.ScreenPointToRay(Input.mousePosition);
        GameObject bulletInstance = Instantiate(bullet, Camera.main.transform.position, Quaternion.identity);
        bulletInstance.SetActive(true);
        bulletInstance.GetComponent<Rigidbody>().AddForce(r.direction * bulletSpeed);
        Destroy(bulletInstance,3f);
    }
}
