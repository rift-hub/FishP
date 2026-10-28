using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CoastalLife : MonoBehaviour
{
    public GameObject crabPrefab, gullPrefab;
    public Material foamMaterial;

    class Walker
    {
        public Transform root;
        public Vector3 origin, target;
        public Transform[] legs;
        public Quaternion[] rest;
        public float phase, travel, pause;
    }

    readonly List<Walker> crabs = new List<Walker>();
    readonly List<Walker> gulls = new List<Walker>();

    public int CrabCount => crabs.Count;
    public int GullCount => gulls.Count;

    void Start()
    {
        var game = GetComponent<TidehavenGame>();
        if (game)
        {
            foreach (var fish in game.fish)
                if (fish && !fish.GetComponent<FishTailMotion>())
                    fish.gameObject.AddComponent<FishTailMotion>();
        }

        int n = 0;
        var shoreCrabs = FindObjectsByType<Transform>()
            .Where(t => t.name == "Crab" && (!t.parent || t.parent.name != "Crab")).ToArray();

        foreach (var t in shoreCrabs)
        {
            if (!crabPrefab) break;
            var obj = Instantiate(crabPrefab, t.position, t.rotation);
            obj.name = "Walking shore crab";
            obj.transform.localScale = Vector3.one * 0.28f;
            foreach (var r in t.GetComponentsInChildren<Renderer>()) r.enabled = false;

            var legs = obj.GetComponentsInChildren<Transform>()
                .Where(x => x.name.Contains("Jointed_Leg")).ToArray();
            var rest = new Quaternion[legs.Length];
            for (int i = 0; i < legs.Length; i++) rest[i] = legs[i].localRotation;

            crabs.Add(new Walker
            {
                root = obj.transform,
                origin = t.position,
                legs = legs,
                rest = rest,
                phase = n++ * 1.7f
            });
        }

        for (int i = 0; i < 4; i++)
        {
            if (!gullPrefab) break;
            Vector3 p = new Vector3(-4f + i * 2.8f, 3.6f + (i % 2) * 0.55f, 0.5f + (i % 2) * 1.2f);
            var obj = Instantiate(gullPrefab, p, Quaternion.Euler(0f, i * 77f, 0f));
            obj.name = "Circling seagull";
            obj.transform.localScale = Vector3.one * 0.95f;
            var wings = obj.GetComponent<WingMotion>();
            if (!wings) wings = obj.AddComponent<WingMotion>();
            wings.phaseOffset = i * 2.3f;
            gulls.Add(new Walker { root = obj.transform, origin = p, phase = i * 2.3f });
        }
    }

    void Update()
    {
        float time = Time.time;
        float dt = Time.deltaTime;

        for (int i = 0; i < crabs.Count; i++)
        {
            var c = crabs[i];
            if (!c.root) continue;
            float moved = 0f;
            if (time >= c.pause)
            {
                if (c.target == Vector3.zero || (c.root.position - c.target).sqrMagnitude < 0.0225f)
                {
                    float x = Random.Range(-22f, 22f);
                    float z = TidehavenGame.Shore(x) + Random.Range(0.5f, 3.4f);
                    c.target = LimbContact.Ground(new Vector3(x, 0f, z), 0.025f);
                    c.pause = time + Random.Range(0.8f, 2.5f);
                }
                else
                {
                    Vector3 before = c.root.position;
                    Vector3 next = Vector3.MoveTowards(before, c.target, dt * 0.42f);
                    next = LimbContact.Ground(next, 0.025f);
                    c.root.position = next;
                    moved = Vector3.Distance(before, next);
                    Vector3 dir = c.target - before;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.001f)
                        c.root.rotation = Quaternion.Slerp(
                            c.root.rotation,
                            Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 90f, 0f),
                            dt * 2.5f);
                }
            }
            if (moved > 0f)
            {
                c.travel += moved * 22f;
                for (int j = 0; j < c.legs.Length; j++)
                {
                    float phase = c.travel + j * Mathf.PI * 0.7f;
                    c.legs[j].localRotation = c.rest[j] * Quaternion.Euler(Mathf.Sin(phase) * 18f, Mathf.Cos(phase) * 10f, 0f);
                }
            }
        }

        for (int i = 0; i < gulls.Count; i++)
        {
            var g = gulls[i];
            if (!g.root) continue;
            float a = time * 0.16f + g.phase;
            float sinA = Mathf.Sin(a), cosA = Mathf.Cos(a);
            Vector3 p = g.origin + new Vector3(sinA * 8.5f, Mathf.Sin(a * 0.7f) * 0.45f, cosA * 4.2f);
            Vector3 dir = new Vector3(cosA * 8.5f, Mathf.Cos(a * 0.7f) * 0.315f, -sinA * 4.2f);
            g.root.position = p;
            g.root.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 0f, -9f) * Quaternion.Euler(0f, 90f, 0f);
        }
    }
}
