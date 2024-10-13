using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Transform))]
[RequireComponent(typeof(Rigidbody))]
public class ReconTransform : NetworkBehaviour
{
    private NetworkVariable<Vector3> _targetDestination;
    private Rigidbody _targetRigidbody;
    private NetworkVariable<Vector3> _targetVelocity;
    private NetworkObject networkObject;
    private bool _targetDestinationChanged;
    private bool _targetVelocityChanged;

    public List<Force> forces;

    public bool isDoneLerping;
    private IEnumerator _Recon;
    private IEnumerator _LerpTarget;
    public List<IEnumerator> movementSnapshots;

    private bool _isReady = false;
    public struct Movement
    {
        public float idealDistance;
        public bool needsRecon;
    }
    public struct Force
    {
        public Vector3 dir;
        public ForceMode forceMode;
    }
    private void Awake()
    {
        isDoneLerping = true;
        forces = new List<Force>();
        _targetRigidbody = GetComponent<Rigidbody>();

        networkObject = GetComponentInParent<NetworkObject>();

        _targetVelocity = new NetworkVariable<Vector3>(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

        _targetDestination = new NetworkVariable<Vector3>(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

        _targetDestinationChanged = false;
        _targetVelocityChanged = false;
        _targetDestination.OnValueChanged += _TargetDestinationChanged;
        _targetVelocity.OnValueChanged += _TargetVelocityChanged;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _isReady = true;
    }

    private void _TargetVelocityChanged(Vector3 previousValue, Vector3 newValue)
    {
        _targetVelocityChanged = true;
    }

    private void _TargetDestinationChanged(Vector3 previousValue, Vector3 newValue)
    {
        _targetDestinationChanged = true;
    }

    public Vector3 GetTargetDestination()
    {
        return _targetDestination.Value;
    }

    [Rpc(SendTo.Server)]
    private void _PollTargetDestinationRpc()
    {
        _targetDestination.Value =
        networkObject
        .gameObject
        .transform
        .position;
    }

    [Rpc(SendTo.Server)]
    public void PollTargetVelocityRpc()
    {
        _targetVelocity.Value =
        networkObject
        .GetComponent<Rigidbody>()
        .velocity;
    }

    [Rpc(SendTo.Server)]
    public void ConsoleMessageRpc(string message)
    {
        //print(message);
    }

    public void OnCollisionEnter(Collision collision)
    {
        // Handles player push
        if (collision.gameObject.tag == "PlayerContainer")
        {
            AddForceRpc(collision.impulse, ForceMode.Impulse);
        }
    }

    public void AddForce(Vector3 dir, ForceMode forceMode)
    {
        // Are we reconciliating?, if yes save the force for later
        if (!IsDoneLerping())
            forces.Add(new Force()
            {
                dir = dir,
                forceMode = forceMode
            });
        else // just apply the force right away
            _targetRigidbody.AddForce(dir, forceMode);
    }

    [Rpc(SendTo.Everyone)]
    public void AddForceRpc(Vector3 dir, ForceMode forceMode, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId == NetworkManager.Singleton.LocalClientId) return;

        AddForce(dir, forceMode);
    }

    public IEnumerator CheckForMovementReconciliation(Func<Vector3, Movement> GetReconData)
    {
        if (!_isReady) yield return null;

        _PollTargetDestinationRpc();

        Movement result = GetReconData(GetTargetDestination());

        if (result.needsRecon)
        {
            _Recon = ReconciliatePosition(result);
            StartCoroutine(_Recon);
        }
    }
    private IEnumerator ReconciliatePosition(Movement reconMovement)
    {
        if (_LerpTarget != null) yield break;

        _LerpTarget = LerpTowardsTarget(reconMovement);
        if (reconMovement.needsRecon)
        {
            _targetDestinationChanged = false;
            _targetVelocityChanged = false;

            // Take note of veloicty before manual movement
            PollTargetVelocityRpc(); // stores in _targetVelocity
            _PollTargetDestinationRpc(); // stores in _targetDestination
            _targetRigidbody.velocity = Vector3.zero;
            _targetRigidbody.useGravity = false;

            // Wait for the client to respond to the change from the server
            while (!_targetDestinationChanged)
                yield return null;

            // Wait for the client to respond to the change from the server
            while (!_targetVelocityChanged)
                yield return null;

            // Perform lerp until done
            isDoneLerping = false;
            StartCoroutine(_LerpTarget);
            while (!IsDoneLerping())
                yield return null;

            // re-apply captured velocity from before reconciliation
            _targetRigidbody.useGravity = true;
            _targetRigidbody.velocity = _targetVelocity.Value;

            // apply any forces that were added during reconciliation
            foreach (Force force in forces)
            {
                ConsoleMessageRpc("Forces Resumed from ReconciliatePosition()");
                _targetRigidbody.AddForce(force.dir, force.forceMode);
            }
            forces.Clear();
        }
        _LerpTarget = null;
        yield return null;
    }
    private bool IsDoneLerping()
    {
        return isDoneLerping;
    }
    private IEnumerator LerpTowardsTarget(Movement reconMovemnet)
    {
        ConsoleMessageRpc("LerpTowardsTarget");
        newPos = transform.position;
        targ = GetTargetDestination();
        reconMovement = reconMovemnet;
        isDoneLerping = false;
        yield return null;
    }

    Vector3 targ;
    Vector3 newPos;
    Vector3 currentVel;
    Movement reconMovement;

    float currentTime = 0;
    float interval = 0.5f;
    private IEnumerator _checkForMovement;
    private void Update()
    {
        if (!_isReady) return;

        // object is out of sync, keep lerping to target position
        if (!isDoneLerping)
        {
            newPos = Vector3.SmoothDamp(newPos, targ, ref currentVel, 0.3f);
            transform.position = newPos;

            if (Vector3.Distance(newPos, targ) <= reconMovement.idealDistance)
            {
                isDoneLerping = true;
            }
        }
        else
        {
            // Every interval we want to check if the object is out of sync
            if (!IsServer && currentTime >= interval && isDoneLerping)
            {
                _checkForMovement = CheckForMovementReconciliation(DefaultCalculateReconData);
                StartCoroutine(_checkForMovement);
                currentTime = 0;
            }
            else
            {
                currentTime += Time.deltaTime;
            }
        }
    }

    Movement DefaultCalculateReconData(Vector3 serverTarget)
    {
        float minDistance = 2;

        return new Movement()
        {
            needsRecon = Vector3.Distance(serverTarget, transform.position) >= minDistance,
            idealDistance = 1,
        };
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        StopAllCoroutines();
    }
}
