using UnityEngine;

namespace SebraeLab.Missions
{
    public class LabPlaza : MonoBehaviour
    {
        Transform plaza;
        Material sky;
        MaterialPropertyBlock colors;

        GameObject Part(string name, Vector3 position, Vector3 size, Color color, PrimitiveType type = PrimitiveType.Cube)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(plaza, false);
            part.transform.position = position;
            part.transform.localScale = size;
            colors.Clear();
            var renderer = part.GetComponent<Renderer>();
            string finish = name switch {
                "Calçada da entrada" or "Alameda central" => "Granilite",
                "Assento" or "Encosto" => "Carvalho",
                "Pé" => "Aco",
                "Canteiro" => "Pedra",
                _ => null
            };
            if (finish != null)
                renderer.sharedMaterial = Resources.Load<Material>("Acabamentos/" + finish);
            if (name == "Copa" || name == "Tronco") {
                colors.SetTexture("_MainTex", Resources.Load<Texture2D>("Praca/" + (name=="Copa" ? "Folhagem" : "Casca")));
                color = Color.white;
            }
            colors.SetVector("_MainTex_ST", new Vector4(Mathf.Max(1,size.x), Mathf.Max(1,size.z),0,0));
            colors.SetFloat("_Glossiness", .13f);
            colors.SetColor("_Color", color);
            part.GetComponent<Renderer>().SetPropertyBlock(colors);
            return part;
        }

        public void Build()
        {
            colors = new MaterialPropertyBlock();
            plaza = new GameObject("Praça do Sebrae Lab").transform;
            plaza.SetParent(transform, true);
            var lawn = new Color(.32f,.52f,.33f);
            var stone = new Color(.82f,.76f,.64f);
            var wood = new Color(.48f,.28f,.15f);
            var leaf = new Color(.17f,.46f,.32f);
            Part("Calçada da entrada", new Vector3(0,-.13f,13), new Vector3(30,.24f,12), stone);
            Part("Alameda central", new Vector3(0,-.11f,20), new Vector3(25,.2f,5), stone);
            for (int side=-1; side<=1; side+=2)
            {
                Part("Canteiro", new Vector3(side*8,.05f,15), new Vector3(8,.15f,3.8f), lawn);
                foreach (float x in new[]{5.6f,10.4f})
                {
                    var basePoint = new Vector3(side*x,0,15.3f);
                    Part("Tronco",basePoint+Vector3.up*1.2f,new Vector3(.42f,1.2f,.42f),wood,PrimitiveType.Cylinder);
                    for(int branch=0;branch<8;branch++){
                        float angle=branch*2.4f+x;
                        var spread=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(branch==7?0:1.02f);
                        var crown=basePoint+Vector3.up*(2.8f+.2f*(branch%3)+(branch==7?.9f:0))+spread;
                        var fork=crown-(basePoint+Vector3.up*1.5f);
                        var limb=Part("Tronco",crown-fork*.5f,new Vector3(.17f,fork.magnitude/2,.17f),wood,PrimitiveType.Cylinder);
                        limb.transform.up=fork.normalized;
                        Part("Copa",crown,new Vector3(1.92f,1.65f,1.77f),leaf,PrimitiveType.Sphere);
                    }
                }
                var seat = new Vector3(side*7,.49f,18);
                Part("Assento",seat,new Vector3(2,.13f,.5f),wood);
                Part("Encosto",seat+new Vector3(0,.29f,-.22f),new Vector3(2,.45f,.09f),wood);
                foreach(float x in new[]{-.7f,.7f})
                    Part("Pé",seat+new Vector3(x,-.23f,0),new Vector3(.14f,.43f,.42f),Color.gray);
            }
            for(int i=0;i<32;i++)
            {
                float angle=i*Mathf.PI/16;
                var h=4+(i*7%11);
                var block=Part("Horizonte urbano",new Vector3(Mathf.Sin(angle)*95,h*.5f-1,Mathf.Cos(angle)*95), new Vector3(18,h,9),new Color(.49f,.61f,.66f));
                block.transform.rotation=Quaternion.Euler(0,angle*Mathf.Rad2Deg,0);
            }
            var shader=Shader.Find("Skybox/Procedural");
            if(shader)
            {
                sky=new Material(shader);
                sky.SetColor("_SkyTint",new Color(.48f,.59f,.72f));
                sky.SetColor("_GroundColor",new Color(.52f,.64f,.66f));
                sky.SetFloat("_SunDisk",0);
                RenderSettings.skybox=sky;
                foreach(var cam in Camera.allCameras) cam.clearFlags=CameraClearFlags.Skybox;
            }
        }
        void OnDestroy(){if(sky) Destroy(sky);}
    }
}
