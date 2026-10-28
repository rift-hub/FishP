using UnityEngine;

// Keep the split jacket panels outside the posed legs after animation and foot IK.
[DefaultExecutionOrder(230), DisallowMultipleComponent]
public sealed class JacketLegFollow : MonoBehaviour
{
    public TidehavenGame game;

    sealed class Panel
    {
        public Transform upperLeg, lowerLeg, coat, tip;
        public Quaternion upperToCoat, lowerToTip;
        public Vector3 coatAnchor, tipAnchor;
    }

    readonly Panel[] panels = new Panel[2];

    void Awake()
    {
        if (!game) game = GetComponent<TidehavenGame>();
        if (!game || !game.visual) return;
        var bones = game.visual.GetComponentsInChildren<Transform>();
        Transform Find(string name) => System.Array.Find(bones, bone => bone.name == name);
        for (int i = 0; i < panels.Length; i++)
        {
            string side = i == 0 ? "Left" : "Right";
            var p = new Panel
            {
                upperLeg = Find(side + "UpperLeg"), lowerLeg = Find(side + "LowerLeg"),
                coat = Find(side + "Coat"), tip = Find(side + "CoatTip")
            };
            if (!p.upperLeg || !p.lowerLeg || !p.coat || !p.tip) continue;
            p.upperToCoat = Quaternion.Inverse(p.upperLeg.rotation) * p.coat.rotation;
            p.lowerToTip = Quaternion.Inverse(p.lowerLeg.rotation) * p.tip.rotation;
            p.coatAnchor = p.upperLeg.InverseTransformPoint(p.coat.position);
            p.tipAnchor = p.lowerLeg.InverseTransformPoint(p.tip.position);
            panels[i] = p;
        }
    }

    void LateUpdate()
    {
        foreach (var p in panels)
        {
            if (p == null) continue;
            // Keep the authored clearance around each leg segment. The waist vertices
            // blend to Hips, while the hem follows the knee's actual pivot after IK.
            p.coat.SetPositionAndRotation(p.upperLeg.TransformPoint(p.coatAnchor), p.upperLeg.rotation * p.upperToCoat);
            p.tip.SetPositionAndRotation(p.lowerLeg.TransformPoint(p.tipAnchor), p.lowerLeg.rotation * p.lowerToTip);
        }
    }
}
