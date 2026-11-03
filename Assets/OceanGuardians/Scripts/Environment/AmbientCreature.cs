using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using OceanGuardians.Core;

namespace OceanGuardians.Environment
{
    /// <summary>
    /// Ambient beach life. Purely decorative: no rewards, no saving.
    ///  - Ghost Crab: scuttles sideways between rocks/tidepools, pauses, and digs into the sand when
    ///    Coral comes close, resurfacing once she has moved away.
    ///  - Sanderling: follows the wave front pecking at wet sand, scampers up the beach from incoming
    ///    waves and away from Coral.
    /// Needs a NavMeshAgent on the root and the model on a child "visual" (that's what digs/pecks/hops).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class AmbientCreature : MonoBehaviour
    {
        public enum Species { GhostCrab, Sanderling }

        [SerializeField] Species species;
        [SerializeField] Transform visual;

        [Header("Senses")]
        [Tooltip("Coral closer than this = hide (crab) / scamper off (bird).")]
        [SerializeField] float fleeDistance = 3f;
        [Tooltip("Crab resurfaces once Coral is farther than this.")]
        [SerializeField] float safeDistance = 5.5f;

        [Header("Movement")]
        [SerializeField] float walkSpeed = 2.5f;
        [SerializeField] float fleeSpeed = 6f;
        [SerializeField] Vector2 pauseSeconds = new(1.5f, 4f);
        [Tooltip("Wander radius around the spawn point when there are no habitat points / waves.")]
        [SerializeField] float wanderRadius = 6f;

        [Header("Crab")]
        [SerializeField] float digDepth = 0.4f;
        [SerializeField] float digSpeed = 1.5f;

        [Header("Sanderling")]
        [Tooltip("Stay this far up the beach from the wave front while foraging.")]
        [SerializeField] float foragingGap = 0.6f;
        [Tooltip("An incoming wave closer than this sends the bird running.")]
        [SerializeField] float waveDangerDistance = 1.5f;

        NavMeshAgent agent;
        Transform player;
        readonly List<Vector3> habitat = new();
        Vector3 home;
        Vector3 visualBase;
        float nextDecision;
        bool hidden, fleeing, waiting;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (visual == null) visual = transform;
            visualBase = visual.localPosition;
            agent.speed = walkSpeed;
            agent.updateRotation = species != Species.GhostCrab; // crabs face sideways, set by hand
        }

        /// <summary>Called by the spawner with the rocks/tidepools crabs travel between.</summary>
        public void Init(IEnumerable<Vector3> habitatPoints)
        {
            habitat.Clear();
            habitat.AddRange(habitatPoints);
        }

        void Start()
        {
            home = transform.position;
            var gm = GameManager.Instance;
            if (gm && gm.Player) player = gm.Player.transform;
        }

        void Update()
        {
            if (!agent.isOnNavMesh) return;
            float playerDist = player ? FlatDistance(player.position) : float.MaxValue;

            if (species == Species.GhostCrab) UpdateCrab(playerDist);
            else UpdateBird(playerDist);
        }

        // ---------- Ghost Crab ----------

        void UpdateCrab(float playerDist)
        {
            float targetY = visualBase.y;
            if (hidden)
            {
                targetY -= digDepth;
                if (playerDist > safeDistance) hidden = false;
            }
            else if (playerDist < fleeDistance)
            {
                hidden = true;
                agent.ResetPath();
            }
            else
            {
                if (Arrived()) PauseThen(() => GoTo(NextCrabPoint(), walkSpeed));
                FaceSideways();
            }

            var lp = visual.localPosition;
            lp.y = Mathf.MoveTowards(lp.y, targetY, digSpeed * Time.deltaTime);
            visual.localPosition = lp;
        }

        Vector3 NextCrabPoint()
        {
            var center = habitat.Count > 0 ? habitat[Random.Range(0, habitat.Count)] : home;
            var r = Random.insideUnitCircle * (habitat.Count > 0 ? 1.5f : wanderRadius);
            return center + new Vector3(r.x, 0f, r.y);
        }

        /// <summary>Turn so the crab's side leads: classic sideways scuttle.</summary>
        void FaceSideways()
        {
            var v = agent.velocity;
            v.y = 0f;
            if (v.sqrMagnitude < 0.01f) return;
            var facing = Quaternion.LookRotation(Vector3.Cross(Vector3.up, v.normalized));
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, 720f * Time.deltaTime);
        }

        // ---------- Sanderling ----------

        void UpdateBird(float playerDist)
        {
            var waves = ShoreWaves.Instance;
            bool waveIncoming = waves && waves.IsSurging && waves.DryDistance(transform.position) < waveDangerDistance;

            if (fleeing && !Arrived()) { /* keep running */ }
            else if (playerDist < fleeDistance)
            {
                var away = transform.position - player.position;
                away.y = 0f;
                GoTo(transform.position + away.normalized * 4f + Random.insideUnitSphere, fleeSpeed);
                fleeing = true;
            }
            else if (waveIncoming)
            {
                GoTo(transform.position - waves.SeaDirection * (waveDangerDistance + 1.5f), fleeSpeed);
                fleeing = true;
            }
            else if (Arrived())
            {
                fleeing = false;
                PauseThen(() =>
                {
                    Vector3 target;
                    if (waves)
                    {
                        // Hop along the shore and down to the freshly-wet sand behind the retreating wave.
                        var alongShore = Vector3.Cross(Vector3.up, waves.SeaDirection) * Random.Range(-3f, 3f);
                        target = waves.ClosestFrontPoint(transform.position + alongShore) - waves.SeaDirection * foragingGap;
                    }
                    else
                    {
                        var r = Random.insideUnitCircle * wanderRadius;
                        target = home + new Vector3(r.x, 0f, r.y);
                    }
                    GoTo(target, walkSpeed);
                });
            }

            // Little hop while running, quick pecks while standing.
            float speed = agent.velocity.magnitude;
            var lp = visualBase;
            float peck = 0f;
            if (speed > 0.3f) lp.y += Mathf.Abs(Mathf.Sin(Time.time * (fleeing ? 30f : 18f))) * 0.06f;
            else peck = Mathf.Max(0f, Mathf.Sin(Time.time * 9f + home.x)) * 45f;
            visual.localPosition = lp;
            visual.localRotation = Quaternion.Euler(peck, 0f, 0f);
        }

        // ---------- Shared ----------

        /// <summary>On arrival: wait a random pause, then run <paramref name="next"/> once.</summary>
        void PauseThen(System.Action next)
        {
            if (!waiting)
            {
                waiting = true;
                nextDecision = Time.time + Random.Range(pauseSeconds.x, pauseSeconds.y);
            }
            else if (Time.time >= nextDecision)
            {
                waiting = false;
                next();
            }
        }

        void GoTo(Vector3 target, float speed)
        {
            if (!NavMesh.SamplePosition(target, out var hit, 2.5f, NavMesh.AllAreas)) return;
            agent.speed = speed;
            agent.SetDestination(hit.position);
        }

        bool Arrived() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f;

        float FlatDistance(Vector3 p)
        {
            var d = p - transform.position;
            d.y = 0f;
            return d.magnitude;
        }
    }
}
