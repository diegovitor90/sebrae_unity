using System;
using System.Collections.Generic;
using UnityEngine;

namespace SebraeLab.Missions
{
    internal sealed class MissionSurfaces
    {
        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        Mesh pinMesh, cupMesh, square;

        Material Get(string name)
        {
            if (!materials.TryGetValue(name, out var material))
            {
                material = Resources.Load<Material>("Acabamentos/" + name);
                if (!material) throw new InvalidOperationException("Material ausente: " + name);
                materials.Add(name, material);
            }
            return material;
        }

        GameObject Detail(Transform parent, string name, Vector3 pos, Vector3 scale, string finish, PrimitiveType type = PrimitiveType.Cube)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            var collider = part.GetComponent<Collider>();
            collider.enabled = false;
            UnityEngine.Object.Destroy(collider);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = pos;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = Get(finish);
            return part;
        }

        // UVs span the whole printed face independently of the primitive's UV layout.
        void Print(Transform parent, string material, Vector3 pos, Quaternion rotation)
        {
            if (!square)
            {
                square = new Mesh { name = "Face impressa" };
                square.vertices = new[]{new Vector3(-.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(.5f,0,-.5f)};
                square.uv = new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};
                square.triangles = new[]{0,1,2,0,2,3};
                square.RecalculateNormals();square.RecalculateBounds();
            }
            var face = new GameObject(material, typeof(MeshFilter), typeof(MeshRenderer));
            face.transform.SetParent(parent,false);face.transform.localPosition=pos;face.transform.localRotation=rotation;
            face.GetComponent<MeshFilter>().sharedMesh=square;
            face.GetComponent<MeshRenderer>().sharedMaterial=Get(material);
        }

        static Mesh Revolve(string name, Vector2[] profile)
        {
            const int sides=40;
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
            float low=profile[0].y, height=profile[profile.Length-1].y-low;
            foreach(var ring in profile)
                for(int i=0;i<=sides;i++){
                    float u=i/(float)sides, angle=u*Mathf.PI*2;
                    vertices.Add(new Vector3(Mathf.Cos(angle)*ring.x,ring.y,Mathf.Sin(angle)*ring.x));
                    uv.Add(new Vector2(u,(ring.y-low)/height));
                }
            for(int j=0;j<profile.Length-1;j++)
                for(int i=0;i<sides;i++){
                    int a=j*(sides+1)+i,b=a+sides+1;
                    indices.AddRange(new[]{a,b,a+1,a+1,b,b+1});
                }
            var mesh=new Mesh {name=name};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);
            mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        void Table(GameObject obj, string top)
        {
            obj.GetComponent<MeshRenderer>().enabled=false;
            var p=obj.transform;
            Detail(p,"Tampo",new Vector3(0,.46f,0),new Vector3(1,.08f,1),top);
            foreach(float x in new[]{-.43f,.43f})
                foreach(float z in new[]{-.39f,.39f})
                    Detail(p,"Pé de aço",new Vector3(x,-.04f,z),new Vector3(.045f,.92f,.06f),"Aco");
        }

        void Board(Transform p,string name,Vector3 pos,Vector3 size,string material){
            var part=Detail(p,name,pos,size,material);part.AddComponent<BoxCollider>();
        }
        void Book(Transform p,string cover,string spine){
            foreach(float y in new[]{-.50f,.50f})
                Detail(p,"Encadernação",Vector3.up*y,new Vector3(1.035f,.045f,1.025f),cover);
            Print(p,cover,Vector3.up*.527f,Quaternion.identity);
            var edge=Detail(p,"Lombada",Vector3.back*.496f,new Vector3(1.02f,1.04f,.025f),spine);
            Print(edge.transform,spine,Vector3.back*.55f,Quaternion.Euler(-90,0,0));
        }
        public void Apply(GameObject obj, string name)
        {
            var renderer=obj.GetComponent<MeshRenderer>();
            var p=obj.transform;
            string finish="Carvalho";
            if(name.StartsWith("Volume ")){
                int index=int.Parse(name.Substring(7))%6;
                Book(p,"Volume"+index,"Lombo"+index);renderer.sharedMaterial=Get("Paginas");return;
            }
            switch(name)
            {
                case "Pino":
                    if(!pinMesh)pinMesh=Revolve("Pino torneado",new[]{new Vector2(0,-1),new Vector2(.30f,-1),new Vector2(.43f,-.89f),new Vector2(.53f,-.56f),new Vector2(.55f,-.33f),new Vector2(.48f,-.08f),new Vector2(.29f,.20f),new Vector2(.19f,.42f),new Vector2(.20f,.58f),new Vector2(.30f,.74f),new Vector2(.31f,.85f),new Vector2(.24f,.95f),new Vector2(0,1)});
                    obj.GetComponent<MeshFilter>().sharedMesh=pinMesh;finish="Pino";break;
                case "Copo de café":
                    if(!cupMesh)cupMesh=Revolve("Copo cônico",new[]{new Vector2(0,-1),new Vector2(.34f,-1),new Vector2(.36f,-.94f),new Vector2(.49f,.90f),new Vector2(.51f,.96f),new Vector2(.49f,1),new Vector2(.46f,1)});
                    obj.GetComponent<MeshFilter>().sharedMesh=cupMesh;
                    Detail(p,"Café e crema",new Vector3(0,.91f,0),new Vector3(.94f,.024f,.94f),"Cafe",PrimitiveType.Cylinder);
                    finish="Copo";break;
                case "Luminária":finish="LuminariaAcesa";break;
                case "Pé de aço":finish="Aco";break;
                case "Mesa do escritório":case "Mesa de inovação":case "Mesa de coworking":case "Mesa da sala de aula":Table(obj,"Carvalho");return;
                case "Balcão da copa":
                    renderer.enabled=false;
                    Detail(p,"Armário",new Vector3(0,-.04f,0),new Vector3(.97f,.92f,.96f),"Carvalho");
                    Detail(p,"Tampo mineral",new Vector3(0,.46f,0),new Vector3(1,.08f,1),"Pedra");
                    finish="Carvalho";break;
                case "Prateleira":
                    renderer.enabled=false; obj.GetComponent<BoxCollider>().enabled=false;
                    foreach(float y in new[]{-.46f,0,.46f})Board(p,"Prateleira de carvalho",new Vector3(0,y,0),new Vector3(1,.08f,1),"Carvalho");
                    foreach(float x in new[]{-.48f,.48f})Board(p,"Lateral",new Vector3(x,0,0),new Vector3(.04f,1,1),"Carvalho");return;
                case "Livro":case "Cartilha de inovação":
                    var cover=name=="Livro"?"Livro":"Cartilha";
                    Book(p,name=="Livro"?"Volume0":cover,"Lombo0");finish="Paginas";break;
                case "Cadeira do escritório":case "Cadeira":
                    renderer.enabled=false;Detail(p,"Assento",Vector3.up*.38f,new Vector3(1,.24f,1),"Tecido");
                    foreach(float x in new[]{-.38f,.38f})foreach(float z in new[]{-.36f,.36f})
                        Detail(p,"Pé",new Vector3(x,-.12f,z),new Vector3(.07f,.76f,.07f),"Aco");return;
                case "Encosto":finish="Tecido";break;
                case "Notebook":Print(p,"Teclado",Vector3.up*.503f,Quaternion.Euler(0,180,0));finish="Aco";break;
                case "Tela":
                    Print(p,"Tela",Vector3.forward*.51f,Quaternion.AngleAxis(180,Vector3.forward)*Quaternion.Euler(90,0,0));finish="Borracha";break;
                case "Pista de boliche":Print(p,"Pista",Vector3.up*.51f,Quaternion.identity);finish="Carvalho";break;
                case "Bola de boliche":
                    foreach (var direction in new[]{new Vector3(-.16f,.82f,-.52f),new Vector3(.19f,.84f,-.50f),new Vector3(.06f,.58f,-.81f)}){
                        var socket=Detail(p,"Encaixe de dedo",direction.normalized*.496f,new Vector3(.085f,.004f,.085f),"Borracha",PrimitiveType.Cylinder);
                        socket.transform.up=direction.normalized;
                    }
                    finish="Bola";break;
                case "Alvo luminoso":
                    var disc=Detail(p,"Face do alvo",Vector3.back*.5f,new Vector3(1f,.001f,1f),"Alvo",PrimitiveType.Cylinder);
                    disc.transform.localRotation=Quaternion.Euler(-90,0,0);finish="Borracha";break;
                case "Porta • sala do estagiário":
                    foreach(float side in new[]{-.5f,.5f}){
                        Detail(p,"Almofada inferior",new Vector3(0,-.26f,side),new Vector3(.78f,.33f,.23f),"Nogueira");
                        Detail(p,"Almofada superior",new Vector3(0,.20f,side),new Vector3(.78f,.45f,.23f),"Nogueira");
                    }
                    finish="Nogueira";break;
                case "Quadro Redes":case "Quadro Circuitos":
                    var art=Detail(p,"Gravura",new Vector3(0,0,.54f),new Vector3(.92f,.92f,.12f),"Borracha");
                    var face=Detail(art.transform,"Impressão",Vector3.forward*.55f,Vector3.one,name.Substring(7),PrimitiveType.Quad);
                    face.transform.localRotation=Quaternion.Euler(0,180,0);
                    finish="Nogueira";break;
                case "Prancha de carvalho":finish="Carvalho";break;
                case "Batente":finish="Nogueira";break;
                case "Suporte da bola":case "Suporte do arco":finish="Carvalho";break;
                case "Maçaneta interna":case "Maçaneta":case "Protótipo":case "Ponta do arco":finish="Aco";break;
                case "Parede lateral":finish="Parede";break;
                case "Parede de fundo":finish="Ripado";break;
                case "Galeria da praça":case "Piso do escritório":case "Galeria de descompressão":case "Ala de atividades":case "Sala de descompressão":finish="Granilite";break;
                case "Lâmina laminada":finish="Carvalho";break;
                case "Lateral da calha":case "Trilho do berço":finish="Aco";break;
                case "Teto preto":case "Corda do arco":case "Fundo da calha":finish="Borracha";break;
                case "Arco arcade":
                    for(int wrap=0;wrap<6;wrap++)
                        Detail(p,"Empunhadura",Vector3.up*(wrap-2.5f)*.047f,new Vector3(1.09f,.036f,1.1f),"Borracha");
                    finish="Nogueira";break;
                case "Estrela arcade":finish="Aco";break;
                case "Rastro":finish="Carvalho";break;
                default:if(name.StartsWith("Destino •") || name=="Marca de percurso")finish="Sinal";else if(name==name.ToUpperInvariant())finish="Placa";break;
            }
            renderer.sharedMaterial=Get(finish);
        }
    }
}
