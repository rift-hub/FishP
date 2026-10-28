using UnityEngine;
using System.Linq;
public class LighthouseWildlife : MonoBehaviour {
 public bool flying; public float phase; public Vector3 origin;
 Transform[] legs;Quaternion[] rest;
 void Start(){origin=transform.position;legs=GetComponentsInChildren<Transform>().Where(t=>t.name.Contains("Jointed_Leg")).ToArray();rest=legs.Select(t=>t.localRotation).ToArray();}
 void Update(){float t=Time.time;
 if(flying){float a=t*.13f+phase;var p=new Vector3(Mathf.Cos(a)*23,3.4f+Mathf.Sin(a*.8f+phase)*.65f+phase*.08f,Mathf.Sin(a)*20);var dir=new Vector3(-Mathf.Sin(a)*23,Mathf.Cos(a*.8f+phase)*.52f,Mathf.Cos(a)*20);transform.position=p;transform.rotation=Quaternion.LookRotation(dir)*Quaternion.Euler(0,90,-8);}
 else {float a=phase+Mathf.Sin(t*.16f+phase)*.12f;var p=LighthouseIslandShape.Beach(a,2.2f+.5f*Mathf.Sin(t*.3f+phase));transform.position=p+Vector3.up*.025f;transform.rotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);for(int i=0;i<legs.Length;i++)legs[i].localRotation=rest[i]*Quaternion.Euler(Mathf.Sin(t*7+i*2)*14,0,Mathf.Cos(t*7+i)*8);}
 }
}
