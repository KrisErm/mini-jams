using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace GodniyGod
{

    public sealed class GGWorld
    {
        public Camera sceneCamera;
        public Transform factoryRoot, dynamicRoot, beltWheel;
        public GGWorldBox[] boxes=new GGWorldBox[3];
        public GGWorldItem[] items=new GGWorldItem[0];
        private Font font;
        private Material steel, floor, wall, pale, wood, dark, cream, glow, belt, gold;
        private Material paperLabel;
        private GGFactoryWear factoryWear;
        private Material wallSurface, floorSurface, housingSurface, beltSurface, worktopSurface;
        private readonly Vector3[] boxPositions={new Vector3(-3.15f,.79f,-1.95f),new Vector3(0,.79f,-1.95f),new Vector3(3.15f,.79f,-1.95f)};
        // Four wide, readable tags instead of six tiny ones. Three pages per delivery.
        public const int ItemsPerPage=4;
        private static readonly float[] slotX={-3.60f,-1.20f,1.20f,3.60f};
        private Color[] seasonColours={new Color(.88f,.67f,.77f),new Color(.54f,.71f,.42f),new Color(.87f,.65f,.34f),new Color(.54f,.76f,.88f)};
        public GGWorld(Font usedFont){font=usedFont;}

        public void SetFactoryYear(int yearIndex)
        {
            if(factoryWear==null)
            {
                var lid=factoryRoot.Find("Крышка входного ящика");
                if(lid==null)return;
                factoryWear=new GGFactoryWear(factoryRoot,sceneCamera,lid);
            }
            factoryWear.SetYear(yearIndex);
        }

        public void ResetFactoryWear(){factoryWear?.SetYear(0);}
        public void DisposeFactoryWear(){factoryWear?.Dispose();factoryWear=null;}

        // The static factory is saved inside Main.unity so that it is visible
        // in Scene view BEFORE Play. Reuse it at runtime instead of spawning twice.
        public bool BindSavedFactory()
        {
            var factory=GameObject.Find("3D Фабрика времени");
            var cam=GameObject.Find("Камера — рабочее место");
            if(factory==null || cam==null) return false;
            factoryRoot=factory.transform;
            sceneCamera=cam.GetComponent<Camera>();
            if(sceneCamera==null) return false;
            var wheel=GameObject.Find("Вращающееся колесо");
            beltWheel=wheel==null?null:wheel.transform;
            InitMaterials();
            var seasonal=GameObject.Find("Сменяемые месяцы и обстоятельства");
            if(seasonal!=null) dynamicRoot=seasonal.transform;
            else
            {
                var preview=GameObject.Find("Сезон — "+GGData.Seasons[0]);
                dynamicRoot=preview==null?null:preview.transform;
            }
            ConfigureConveyorHousings();
            return true;
        }
        private void InitMaterials()
        {
            // V14: normal Lit surfaces. Textured panels are based on the pack's restrained
            // industrial palette; original EKstudio models keep their separate imported look.
            steel=Mat(new Color(.58f,.65f,.74f));floor=Mat(new Color(.24f,.29f,.36f));
            wall=Mat(new Color(.46f,.55f,.65f));pale=Mat(new Color(.89f,.92f,.91f));
            wood=Mat(new Color(.80f,.61f,.42f));dark=Mat(new Color(.13f,.22f,.31f));
            cream=Mat(new Color(.98f,.94f,.83f));glow=Mat(new Color(.94f,.66f,.27f));
            belt=Mat(new Color(.37f,.52f,.67f));gold=Mat(new Color(.96f,.76f,.33f));
            paperLabel=Mat(new Color(.98f,.94f,.84f));
            wallSurface=TexturedOrColoured("panel_wall",new Color(.37f,.44f,.52f),new Vector2(4,2));
            floorSurface=TexturedOrColoured("floor_plates",new Color(.19f,.22f,.28f),new Vector2(5,4));
            housingSurface=TexturedOrColoured("steel_housing",new Color(.55f,.63f,.72f),new Vector2(1,1));
            beltSurface=TexturedOrColoured("belt_rubber",new Color(.33f,.46f,.58f),new Vector2(3,1));
            worktopSurface=TexturedOrColoured("wood_workbench",new Color(.73f,.54f,.37f),new Vector2(3,2));
        }

        public void Build()
        {
            factoryRoot=new GameObject("3D Фабрика времени").transform;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.47f,.49f,.52f);
            RenderSettings.fog=false;
            InitMaterials();
            var camObject=new GameObject("Камера — рабочее место");
            sceneCamera=camObject.AddComponent<Camera>();camObject.tag="MainCamera";
            sceneCamera.transform.position=new Vector3(0,4.85f,-8.0f);
            sceneCamera.transform.LookAt(new Vector3(0,1.62f,.05f));
            sceneCamera.fieldOfView=55f;sceneCamera.nearClipPlane=.1f;sceneCamera.farClipPlane=80f;
            sceneCamera.clearFlags=CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor=new Color(.10f,.15f,.18f);
            sceneCamera.allowHDR=false;  // disable extreme brightness in URP and Built-in
            var listener=camObject.AddComponent<AudioListener>();
            var sun=new GameObject("Общее освещение").AddComponent<Light>();
            sun.type=LightType.Directional;sun.intensity=0.82f;sun.color=new Color(1.0f,.96f,.89f);
            sun.shadows=LightShadows.Soft;sun.shadowStrength=.32f;
            sun.transform.rotation=Quaternion.Euler(46,-32,0);
            PointLight("Тёплая лампа",new Vector3(-3.8f,5.7f,-1.5f),new Color(1,.83f,.65f),0.65f,10);
            PointLight("Лампа конвейера",new Vector3(3.5f,4.7f,1.2f),new Color(.80f,.90f,1.0f),0.55f,10);
            // Dark industrial background, with enough light to read the 3D cards.
            Cube("Пол",new Vector3(0,-.24f,.45f),new Vector3(18,.45f,13),floorSurface,factoryRoot);
            Cube("Задняя стена",new Vector3(0,3.25f,5.6f),new Vector3(17,7,.34f),wallSurface,factoryRoot);
            Cube("Левая стена",new Vector3(-8,3,0),new Vector3(.35f,6,13),wallSurface,factoryRoot);
            Cube("Правая стена",new Vector3(8,3,0),new Vector3(.35f,6,13),wallSurface,factoryRoot);
            for(int i=0;i<6;i++)
            {
                Cube("Балки",new Vector3(-7.5f+i*3,3,5.35f),new Vector3(.18f,6,.3f),steel,factoryRoot);
                Cube("Разметка пола",new Vector3(-7+i*2.7f,-.003f,-4),new Vector3(.09f,.012f,2.4f),gold,factoryRoot);
            }
            // Factory title is part of the UI. Avoid overlapping the 3D wall sign with the HUD.
            // Conveyor frame, rollers and belt. Imported EKstudio model decorates its chassis.
            Cube("Основа конвейера",new Vector3(0,1.13f,.85f),new Vector3(10.2f,.65f,1.9f),housingSurface,factoryRoot);
            Cube("Резиновая лента",new Vector3(0,1.51f,.85f),new Vector3(9.8f,.07f,1.68f),beltSurface,factoryRoot);
            Cube("Ограждение заднее",new Vector3(0,1.67f,1.69f),new Vector3(10,.24f,.15f),pale,factoryRoot);
            Cube("Ограждение переднее",new Vector3(0,1.61f,-.01f),new Vector3(10,.15f,.10f),pale,factoryRoot);
            // A few warm industrial accents break up the cold grey and echo the machine asset.
            Cube("Оранжевая линия безопасности",new Vector3(0,1.48f,-.05f),new Vector3(9.65f,.07f,.04f),glow,factoryRoot);
            Cube("Панель под конвейером",new Vector3(0,.94f,-.13f),new Vector3(9.7f,.22f,.07f),dark,factoryRoot);
            // Visible entry and exit housings so the delivery clearly comes from somewhere and leaves somewhere.
            var crateTunnel=TexturedOrColoured("crate_orange",new Color(.77f,.49f,.26f),new Vector2(2,2));
            Cube("Входной ящик-конвейер",new Vector3(-6.95f,1.36f,.85f),new Vector3(3.15f,1.92f,3.10f),crateTunnel,factoryRoot);
            Cube("Входное окно",new Vector3(-5.70f,1.60f,.85f),new Vector3(1.25f,1.20f,1.75f),belt,factoryRoot);
            Cube("Выходной ящик-конвейер",new Vector3(6.95f,1.36f,.85f),new Vector3(3.15f,1.92f,3.10f),crateTunnel,factoryRoot);
            Cube("Выходное окно",new Vector3(5.70f,1.60f,.85f),new Vector3(1.25f,1.20f,1.75f),belt,factoryRoot);
            ConfigureConveyorHousings();
            for(int i=0;i<13;i++)
            {
                var c=Cylinder("Валик конвейера",new Vector3(-4.65f+i*.78f,1.48f,.85f),new Vector3(.085f,.80f,.085f),steel,factoryRoot);
                c.transform.rotation=Quaternion.Euler(90,0,0);
            }
            for(int i=0;i<4;i++)
                Cube("Ножка конвейера",new Vector3((i<2?-4.5f:4.5f),.52f,(i%2==0?.18f:1.52f)),new Vector3(.24f,1.0f,.24f),steel,factoryRoot);
            Cube("Рабочий стол",new Vector3(0,.65f,-2.05f),new Vector3(10.8f,.25f,3),worktopSurface,factoryRoot);
            for(int i=0;i<4;i++)
                Cube("Ножка стола",new Vector3(i<2?-4.7f:4.7f,.29f,i%2==0?-3.20f:-.85f),new Vector3(.32f,.6f,.32f),steel,factoryRoot);
            for(int i=0;i<3;i++) Cube("Место коробки",boxPositions[i]+new Vector3(0,-.06f,0),new Vector3(2.42f,.15f,2.0f),dark,factoryRoot);
            // Decorative production equipment from the provided low-poly asset pack.
            TryFactoryModel("FactoryModels/factory_machine", "Промышленная машина (EKstudio)",new Vector3(5.9f,.20f,4.35f),new Vector3(2.6f,2.8f,2.1f));
            // Pipes and warning lights add depth without requiring extra imported assets.
            Cube("Шкаф управления",new Vector3(-6.20f,1.60f,4.85f),new Vector3(1.1f,2.7f,.8f),housingSurface,factoryRoot);
            for(int i=0;i<3;i++) Cube("Индикатор",new Vector3(-6.2f,2.35f-i*.34f,4.30f),new Vector3(.31f,.15f,.07f),i==0?glow:pale,factoryRoot);
            for(int i=0;i<2;i++)
            {
                var pipe=Cylinder("Труба",new Vector3(5.5f+i*.55f,3.65f,5.13f),new Vector3(.15f,1.2f,.15f),steel,factoryRoot);
                pipe.transform.rotation=Quaternion.Euler(90,0,0);
            }
            var wheel=Cylinder("Вращающееся колесо",new Vector3(5.65f,2.55f,4.45f),new Vector3(.55f,.12f,.55f),gold,factoryRoot);
            wheel.transform.rotation=Quaternion.Euler(90,0,0);beltWheel=wheel.transform;
            // The belt and visual miniatures identify the delivery. No extra floating sign.
            dynamicRoot=new GameObject("Сменяемые месяцы и обстоятельства").transform;
        }

        // Also update an already-generated V21 scene in place when Play starts.
        private void ConfigureConveyorHousings()
        {
            for(int side=-1;side<=1;side+=2)
            {
                bool inlet=side<0;
                var housing=factoryRoot.Find(inlet?"Входной ящик-конвейер":"Выходной ящик-конвейер");
                var opening=factoryRoot.Find(inlet?"Входное окно":"Выходное окно");
                if(housing==null || opening==null)continue;
                housing.localPosition=new Vector3(side*7.13f,inlet?1.42f:1.54f,.88f);
                housing.localScale=new Vector3(3.46f,inlet?2.04f:2.28f,3.34f);
                opening.localPosition=new Vector3(side*5.55f,1.71f,.86f);
                opening.localScale=new Vector3(.58f,1.57f,1.99f);
                opening.GetComponent<Renderer>().sharedMaterial=dark;
                if(!inlet || factoryRoot.Find("Крышка входного ящика")!=null)continue;

                // Same texture as the original case. The pivot is at the back seam.
                var lid=new GameObject("Крышка входного ящика").transform;
                lid.SetParent(factoryRoot,false);
                lid.localPosition=new Vector3(-7.13f,2.56f,2.38f);
                var bodyMat=housing.GetComponent<Renderer>().sharedMaterial;
                var panel=Cube("Верхняя панель",new Vector3(0,0,-1.50f),
                    new Vector3(3.48f,.24f,3.36f),bodyMat,lid,true);
                panel.GetComponent<Collider>().enabled=false;
            }
        }

        public void BuildSeason(int year,int season,List<GGEvent> events)
        {
            if(dynamicRoot!=null)
            {
#if UNITY_EDITOR
                if(!Application.isPlaying) UnityEngine.Object.DestroyImmediate(dynamicRoot.gameObject);
                else
#endif
                    UnityEngine.Object.Destroy(dynamicRoot.gameObject);
            }
            dynamicRoot=new GameObject("Сезон — "+GGData.Seasons[season]).transform;
            var names=GGData.GetBoxMonths(year,season);
            boxes=new GGWorldBox[3];
            for(int i=0;i<3;i++) boxes[i]=CreateBox(i,names[i],season);
            items=new GGWorldItem[events.Count];
            for(int i=0;i<events.Count;i++) items[i]=CreateItem(i,events[i]);
            ShowPage(0, false);
        }
        public void ShowPage(int page,bool animate=true)
        {
            if(items==null)return;
            for(int i=0;i<items.Length;i++)
            {
                var item=items[i];if(item==null)continue;
                bool visible=i/ItemsPerPage==page&&item.gameObject.name.IndexOf("[УБРАН]",StringComparison.Ordinal)<0;
                item.gameObject.SetActive(visible);
                if(!visible)continue;

                Vector3 target=GetSlotPosition(i%ItemsPerPage);
                item.transform.DOKill(false);
                item.transform.localScale=item.defaultScale;
                if(animate)
                {
                    // Identical distance and duration for every object: the belt moves as a whole.
                    item.transform.position=target+Vector3.left*11f;
                    item.transform.DOMove(target,.68f).SetEase(Ease.Linear);
                }
                else item.transform.position=target;
            }
        }
        /// <summary>
        /// DOTween: the belt follows the pressed arrow. Next page: exit right,
        /// enter from left. Previous page: exit left, enter from right.
        /// All objects move together with the same speed and spacing.
        /// </summary>
        public void SlideToPage(int currentPage,int nextPage,Action onComplete)
        {
            if(items==null){onComplete?.Invoke();return;}

            const float shift=11f;
            const float travelSeconds=.68f;
            float direction=nextPage>currentPage ? 1f : -1f;
            Vector3 exitOffset=Vector3.right*shift*direction;
            Vector3 entryOffset=-exitOffset;
            var outgoing=new List<GGWorldItem>();
            foreach(var item in items)
                if(item!=null&&item.gameObject.activeSelf&&item.index/ItemsPerPage==currentPage)
                    outgoing.Add(item);

            Action enter=() =>
            {
                ShowPage(nextPage,false);
                Sequence arrival=DOTween.Sequence();
                int count=0;
                foreach(var item in items)
                {
                    if(item==null||!item.gameObject.activeSelf||item.index/ItemsPerPage!=nextPage)continue;
                    Vector3 target=GetSlotPosition(item.index%ItemsPerPage);
                    item.transform.DOKill(false);
                    item.transform.localScale=item.defaultScale;
                    item.transform.position=target+entryOffset;
                    // Join at t=0: no stagger, all items move with equal velocity.
                    arrival.Join(item.transform.DOMove(target,travelSeconds).SetEase(Ease.Linear));
                    count++;
                }
                if(count==0){arrival.Kill(false);onComplete?.Invoke();}
                else arrival.OnComplete(() => onComplete?.Invoke());
            };

            if(outgoing.Count==0){enter();return;}
            Sequence departure=DOTween.Sequence();
            foreach(var item in outgoing)
            {
                item.transform.DOKill(false);
                item.transform.localScale=item.defaultScale;
                Vector3 destination=item.transform.position+exitOffset;
                departure.Join(item.transform.DOMove(destination,travelSeconds).SetEase(Ease.Linear));
            }
            departure.OnComplete(() => enter());
        }

        public Vector3 GetSlotPosition(int slot){return new Vector3(slotX[slot],1.78f,.72f);}
        public Vector3 GetBoxDrop(int index){return boxPositions[index]+new Vector3(0,.73f,0);}
        public void RefreshBox(int index,int count)
        {
            if(boxes[index]!=null)
            {
                boxes[index].counter.text=count+"/3";
                if(count>=3)GGAnimation.Shake(boxes[index].transform,.24f);
            }
        }
        public void MarkItemRemoved(GGWorldItem i){if(i!=null)i.gameObject.name+=" [УБРАН]";}
        public void SelectItem(GGWorldItem item,bool selected)
        {
            if(item!=null)GGAnimation.Scale(item.transform,item.defaultScale*(selected?1.11f:1f),.16f);
        }
        public void AnimateCompletion()
        {
            for(int i=0;i<boxes.Length;i++) if(boxes[i]!=null)GGAnimation.Shake(boxes[i].transform,.38f);
            if(beltWheel!=null)GGAnimation.Spin(beltWheel,1f);
        }

        private GGWorldBox CreateBox(int index,string month,int season)
        {
            var root=new GameObject("Коробка месяца — "+month);
            root.transform.SetParent(dynamicRoot);
            root.transform.position=boxPositions[index];
            var box=root.AddComponent<GGWorldBox>();
            box.index=index;box.month=month;
            var hit=root.AddComponent<BoxCollider>();
            hit.center=new Vector3(0,.65f,0);
            hit.size=new Vector3(2.48f,1.8f,2.0f);

            string resource="SeasonBoxes/box_"+GGData.SeasonResources[season];
            var prefab=Resources.Load<GameObject>(resource);
            if(prefab!=null)
            {
                var imported=UnityEngine.Object.Instantiate(prefab,root.transform);
                imported.name="ИСТОЧНИК FBX — один корпус коробки";
                imported.transform.localPosition=Vector3.zero;
                // Side flaps should face left and right, not toward the camera.
                imported.transform.localRotation=Quaternion.Euler(0f,90f,0f);

                // Blender export contains an untouched default Cube (32 vertices),
                // another mesh that is the REAL open box (48/60 vertices), and a Light.
                // Hide the accidental Cube / Camera / Light BEFORE computing bounds.
                int retained=0;
                foreach(var filter in imported.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(filter.sharedMesh==null)continue;
                    bool isRealBox=filter.gameObject.name!="Cube" && filter.sharedMesh.vertexCount>=40;
                    if(!isRealBox)filter.gameObject.SetActive(false);
                    else retained++;
                }
                foreach(var light in imported.GetComponentsInChildren<Light>(true))
                    light.gameObject.SetActive(false);
                foreach(var camera in imported.GetComponentsInChildren<Camera>(true))
                    camera.gameObject.SetActive(false);

                if(retained==0)
                    Debug.LogWarning("Годный год: в FBX не найдена основная геометрия коробки: "+resource);

                // Only remaining box mesh participates in fitting, not the spare Cube.
                FitModel(imported.transform,new Vector3(2.36f,1.52f,1.80f),Vector3.zero);

                Color seasonColor=SeasonBoxColor(season);
                Material bodyMaterial=MatLit(seasonColor);
                Material accentMaterial=MatLit(Color.Lerp(seasonColor,Color.white,.10f));
                foreach(var renderer in imported.GetComponentsInChildren<Renderer>())
                {
                    var materials=renderer.sharedMaterials;
                    for(int n=0;n<materials.Length;n++)
                        materials[n]=(n==0?bodyMaterial:accentMaterial);
                    renderer.sharedMaterials=materials;
                }
                foreach(var col in imported.GetComponentsInChildren<Collider>())col.enabled=false;
            }
            else
            {
                Debug.LogError("Годный год: не найдена FBX модель: "+resource);
                Cube("Временная коробка",new Vector3(0,.48f,0),new Vector3(2.25f,.96f,1.8f),
                    Mat(SeasonBoxColor(season)),root.transform,true);
            }

            // Both labels are OUTSIDE the FBX geometry to avoid flaps covering them.
            Billboard(month.ToUpperInvariant(),root.transform.position+new Vector3(0,.27f,-1.72f),
                new Vector2(1.62f,.36f),37,new Color(.09f,.15f,.19f),root.transform,false,
                new Color(.98f,.94f,.83f,1));

            // Side counter: no longer hides the box front or its season picture.
            Cube("Подложка счётчика справа",new Vector3(1.05f,.69f,-1.43f),
                new Vector3(.69f,.33f,.045f),paperLabel,root.transform,true);
            box.counter=WorldText("0/3",root.transform.position+new Vector3(1.05f,.71f,-1.50f),
                50,new Color(.10f,.16f,.20f),root.transform);
            box.counter.characterSize=.032f;

            Texture2D icon=Resources.Load<Texture2D>("SeasonIcons/"+GGData.SeasonResources[season]);
            if(icon!=null)
            {
                var art=SeasonStickerMaterial(icon,season);
                // Images supplied by the team, one set on the visible face.
                AddSeasonSticker(root.transform,art,new Vector3(-.40f,.32f,-1.38f),new Vector3(0,180,0),new Vector2(.42f,.42f));
                AddSeasonSticker(root.transform,art,new Vector3(.43f,.34f,-1.38f),new Vector3(0,180,0),new Vector2(.52f,.52f));
                AddSeasonSticker(root.transform,art,new Vector3(0f,-.20f,-1.38f),new Vector3(0,180,12),new Vector2(.47f,.47f));
            }
            return box;
        }

        private Color SeasonBoxColor(int season)
        {
            switch(season)
            {
                case 0:return new Color(.89f,.59f,.69f); // spring muted rose
                case 1:return new Color(.58f,.74f,.49f); // summer grass green
                case 2:return new Color(.89f,.68f,.43f); // autumn ochre
                default:return new Color(.52f,.73f,.86f); // winter powder blue
            }
        }

        private Material MatLit(Color tint)
        {
            var shader=PreferredLitShader();
#if UNITY_EDITOR
            if(!Application.isPlaying)
            {
                var c=(Color32)tint;
                string path="Assets/GodniyGod/Materials/GG_V14_Box_"+
                    c.r.ToString("X2")+c.g.ToString("X2")+c.b.ToString("X2")+".mat";
                var saved=UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
                if(saved==null)
                {
                    saved=new Material(shader);
                    UnityEditor.AssetDatabase.CreateAsset(saved,path);
                }
                saved.shader=shader;
                ApplyTint(saved,tint);
                if(saved.HasProperty("_Metallic"))saved.SetFloat("_Metallic",0f);
                if(saved.HasProperty("_Smoothness"))saved.SetFloat("_Smoothness",.15f);
                UnityEditor.EditorUtility.SetDirty(saved);
                return saved;
            }
#endif
            var material=new Material(shader);
            ApplyTint(material,tint);
            if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",0f);
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.15f);
            return material;
        }

        private Material SeasonStickerMaterial(Texture2D picture,int season)
        {
            var shader=Shader.Find("Sprites/Default")??Shader.Find("Unlit/Transparent");
#if UNITY_EDITOR
            if(!Application.isPlaying)
            {
                string path="Assets/GodniyGod/Materials/GG_V10_Icon_"+
                    GGData.SeasonResources[season]+".mat";
                var saved=UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
                if(saved==null)
                {
                    saved=new Material(shader);
                    UnityEditor.AssetDatabase.CreateAsset(saved,path);
                }
                saved.shader=shader;
                saved.mainTexture=picture;
                UnityEditor.EditorUtility.SetDirty(saved);
                return saved;
            }
#endif
            var material=new Material(shader);
            material.mainTexture=picture;
            return material;
        }

        private Material FactoryPaletteMaterial(Texture2D palette)
        {
            var shader=Shader.Find("Unlit/Texture")??Shader.Find("Universal Render Pipeline/Unlit")??PreferredUnlitShader();
#if UNITY_EDITOR
            if(!Application.isPlaying)
            {
                const string path="Assets/GodniyGod/Materials/GG_V10_FactoryPalette.mat";
                var saved=UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
                if(saved==null)
                {
                    saved=new Material(shader);
                    UnityEditor.AssetDatabase.CreateAsset(saved,path);
                }
                saved.shader=shader;
                saved.mainTexture=palette;
                if(saved.HasProperty("_BaseMap"))saved.SetTexture("_BaseMap",palette);
                ApplyTint(saved,Color.white);
                UnityEditor.EditorUtility.SetDirty(saved);
                return saved;
            }
#endif
            var material=new Material(shader);
            material.mainTexture=palette;
            if(material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",palette);
            ApplyTint(material,Color.white);
            return material;
        }

        private void AddSeasonSticker(Transform parent,Material material,Vector3 localPosition,Vector3 rotation,Vector2 size)
        {
            var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name="Ваша сезонная картинка PNG";
            quad.transform.SetParent(parent,false);
            quad.transform.localPosition=localPosition;
            quad.transform.localRotation=Quaternion.Euler(rotation);
            quad.transform.localScale=new Vector3(size.x,size.y,1f);
            quad.GetComponent<Renderer>().sharedMaterial=material;
#if UNITY_EDITOR
            if(!Application.isPlaying)UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
            else
#endif
                UnityEngine.Object.Destroy(quad.GetComponent<Collider>());
        }

        private GGWorldItem CreateItem(int index,GGEvent ev)
        {
            GameObject root=new GameObject("Обстоятельство: "+ev.title);
            root.transform.SetParent(dynamicRoot);
            root.transform.position=GetSlotPosition(index%ItemsPerPage);
            var entry=root.AddComponent<GGWorldItem>();
            entry.eventData=ev;entry.index=index;entry.defaultScale=Vector3.one;
            var collision=root.AddComponent<BoxCollider>();
            collision.size=new Vector3(2.16f,1.77f,1.34f);
            collision.center=new Vector3(0,.64f,0);
            // One NEUTRAL pallet per item. No red/yellow/green score labels.
            Cube("Деревянный поддон",new Vector3(0,.01f,0),
                new Vector3(2.05f,.16f,1.25f),wood,root.transform,true);
            Cube("Площадка для миниатюры",new Vector3(0,.12f,.07f),
                new Vector3(1.9f,.08f,1.10f),paperLabel,root.transform,true);
            GGEventVisuals.Build(ev,root.transform,Mat);
            // Small caption in front of the 3D scene. Never cover the 3D object.
            Billboard(ev.title,root.transform.position+new Vector3(0,.19f,-.75f),
                new Vector2(2.02f,.43f),29,new Color(.12f,.16f,.17f),root.transform,false,
                new Color(.98f,.94f,.85f,1));
            return entry;
        }
        private void TryFactoryModel(string resource,string title,Vector3 position,Vector3 desired)
        {
            GameObject source=Resources.Load<GameObject>(resource);if(source==null)return;
            GameObject model=UnityEngine.Object.Instantiate(source,factoryRoot);model.name=title;
            model.transform.position=position;
            FitModel(model.transform,desired,position);
            // Hide broken/oversized renderers from this optional pack rather than covering the playfield.
            var allRenderers=model.GetComponentsInChildren<Renderer>();
            foreach(var part in allRenderers)
                if(part.bounds.size.magnitude>desired.magnitude*3f)part.enabled=false;
            foreach(var c in model.GetComponentsInChildren<Collider>())c.enabled=false;
            // Force all pack models into one readable low-poly palette so they stop turning white in Unity 6.
            var accentGreen=Mat(new Color(.42f,.88f,.32f));
            var machineBlue=Mat(new Color(.42f,.56f,.72f));
            var orangeAccent=Mat(new Color(1.00f,.64f,.14f));
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                if(!(renderer is MeshRenderer||renderer is SkinnedMeshRenderer))continue;
                var materials=renderer.sharedMaterials;
                for(int j=0;j<materials.Length;j++)
                {
                    int idx=(j + renderer.gameObject.name.Length)%6;
                    materials[j]= idx==0 ? steel : idx==1 ? machineBlue : idx==2 ? pale : idx==3 ? orangeAccent : idx==4 ? accentGreen : dark;
                }
                renderer.sharedMaterials=materials;
            }

        }
        private static void FitModel(Transform model,Vector3 desired,Vector3 localAnchor)
        {
            var renderers=model.GetComponentsInChildren<Renderer>();if(renderers.Length==0)return;
            Bounds b=renderers[0].bounds;
            for(int i=1;i<renderers.Length;i++)b.Encapsulate(renderers[i].bounds);
            float sx=desired.x/Mathf.Max(.001f,b.size.x),sy=desired.y/Mathf.Max(.001f,b.size.y),sz=desired.z/Mathf.Max(.001f,b.size.z);
            float ratio=Mathf.Min(sx,Mathf.Min(sy,sz));
            model.localScale*=ratio;
            var r2=model.GetComponentsInChildren<Renderer>();Bounds after=r2[0].bounds;
            for(int i=1;i<r2.Length;i++)after.Encapsulate(r2[i].bounds);
            // Center on the local target, with the mesh bottom on its support.
            Vector3 target=model.parent.TransformPoint(localAnchor)+new Vector3(0,desired.y*.5f,0);
            model.position+=target-after.center;
        }
        private static Shader PreferredUnlitShader()
        {
            // Different shaders in the Built-in and Universal render pipelines.
            bool srp=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline!=null;
            Shader shader=srp ? Shader.Find("Universal Render Pipeline/Unlit") : Shader.Find("Unlit/Color");
            if(shader==null)shader=Shader.Find("Unlit/Color");
            if(shader==null)shader=Shader.Find("Universal Render Pipeline/Unlit");
            if(shader==null)shader=Shader.Find("Sprites/Default");
            return shader ?? Shader.Find("Standard");
        }
        private static void ApplyTint(Material material,Color colour)
        {
            if(material.HasProperty("_BaseColor")) material.SetColor("_BaseColor",colour);
            if(material.HasProperty("_Color")) material.SetColor("_Color",colour);
        }
        private static Shader PreferredLitShader()
        {
            bool srp=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline!=null;
            Shader shader=srp ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
            return shader ?? Shader.Find("Standard") ?? PreferredUnlitShader();
        }
        private Material TexturedOrColoured(string textureName,Color fallback,Vector2 tiling)
        {
            Texture2D picture=Resources.Load<Texture2D>("FactorySurfaces/"+textureName);
            if(picture==null) return Mat(fallback);
            Shader shader=PreferredLitShader();
#if UNITY_EDITOR
            if(!Application.isPlaying)
            {
                const string folder="Assets/GodniyGod/Materials";
                if(!UnityEditor.AssetDatabase.IsValidFolder(folder))
                    UnityEditor.AssetDatabase.CreateFolder("Assets/GodniyGod","Materials");
                string path=folder+"/GG_V15_Surface_"+textureName+".mat";
                Material saved=UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
                if(saved==null)
                {
                    saved=new Material(shader);
                    UnityEditor.AssetDatabase.CreateAsset(saved,path);
                }
                ConfigureTexturedOrColoured(saved,shader,picture,tiling);
                UnityEditor.EditorUtility.SetDirty(saved);
                return saved;
            }
#endif
            Material runtime=new Material(shader);
            ConfigureTexturedOrColoured(runtime,shader,picture,tiling);
            return runtime;
        }
        private static void ConfigureTexturedOrColoured(Material mat, Shader shader,Texture2D picture,Vector2 tiling)
        {
            mat.shader=shader;
            if(mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap",picture);
                mat.SetTextureScale("_BaseMap",tiling);
            }
            if(mat.HasProperty("_MainTex"))
            {
                mat.SetTexture("_MainTex",picture);
                mat.SetTextureScale("_MainTex",tiling);
            }
            ApplyTint(mat,Color.white);
            if(mat.HasProperty("_Metallic"))mat.SetFloat("_Metallic",0f);
            if(mat.HasProperty("_Smoothness"))mat.SetFloat("_Smoothness",.10f);
        }
        private Material Mat(Color colour)
        {
            var shader=PreferredLitShader();
#if UNITY_EDITOR
            if(!Application.isPlaying)
            {
                var c=(Color32)colour;
                const string folder="Assets/GodniyGod/Materials";
                if(!UnityEditor.AssetDatabase.IsValidFolder(folder))
                    UnityEditor.AssetDatabase.CreateFolder("Assets/GodniyGod","Materials");
                string path=folder+"/GG_V15_"+c.r.ToString("X2")+c.g.ToString("X2")+c.b.ToString("X2")+".mat";
                Material saved=UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
                if(saved==null)
                {
                    saved=new Material(shader);
                    UnityEditor.AssetDatabase.CreateAsset(saved,path);
                }
                saved.shader=shader;
                ApplyTint(saved,colour);
                if(saved.HasProperty("_Metallic"))saved.SetFloat("_Metallic",0f);
                if(saved.HasProperty("_Smoothness"))saved.SetFloat("_Smoothness",.08f);
                UnityEditor.EditorUtility.SetDirty(saved);
                return saved;
            }
#endif
            var material=new Material(shader);
            ApplyTint(material,colour);
            if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",0f);
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.08f);
            return material;
        }
        private GameObject Cube(string name,Vector3 pos,Vector3 scale,Material material,Transform parent,bool local=false)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent);
            if(local)g.transform.localPosition=pos;else g.transform.position=pos;
            g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=material;
            return g;
        }
        private GameObject Cylinder(string name,Vector3 pos,Vector3 scale,Material mat,Transform parent)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=name;g.transform.SetParent(parent);g.transform.position=pos;g.transform.localScale=scale;
            g.GetComponent<Renderer>().sharedMaterial=mat;return g;
        }
        private void PointLight(string name,Vector3 pos,Color colour,float intensity,float range)
        { var l=new GameObject(name).AddComponent<Light>();l.type=LightType.Point;l.color=colour;l.intensity=intensity;l.range=range;l.transform.position=pos;}
        private TextMesh WorldText(string value,Vector3 worldPosition,int size,Color color,Transform parent)
        {
            GameObject obj=new GameObject("3D текст: "+value);obj.transform.SetParent(parent);
            obj.transform.position=worldPosition;obj.transform.rotation=sceneCamera.transform.rotation;
            var t=obj.AddComponent<TextMesh>();t.text=value;t.font=font;t.fontSize=size;t.characterSize=.027f;
            t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=color;
            obj.GetComponent<MeshRenderer>().material=font.material;
            return t;
        }
        private void Billboard(string words,Vector3 position,Vector2 size,int fontSize,Color tint,Transform parent,bool scaleWithParent=true,Color? backgroundColor=null)
        {
            var worldCanvas=new GameObject("3D надпись: "+words,typeof(RectTransform),typeof(Canvas));
            worldCanvas.transform.SetParent(parent);worldCanvas.transform.position=position;
            worldCanvas.transform.rotation=sceneCamera.transform.rotation;
            var rt=worldCanvas.GetComponent<RectTransform>();rt.sizeDelta=new Vector2(size.x*200f,size.y*200f);
            rt.localScale=Vector3.one*.005f;
            var canvas=worldCanvas.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            var backing=new GameObject("Тёмная подложка",typeof(RectTransform),typeof(Image));
            backing.transform.SetParent(worldCanvas.transform,false);
            var br=backing.GetComponent<RectTransform>();br.anchorMin=Vector2.zero;br.anchorMax=Vector2.one;
            br.offsetMin=Vector2.zero;br.offsetMax=Vector2.zero;
            var bg=backing.GetComponent<Image>();bg.color=backgroundColor ?? new Color(.055f,.11f,.17f,1f);bg.raycastTarget=false;
            var go=new GameObject("Текст",typeof(RectTransform),typeof(Text));go.transform.SetParent(worldCanvas.transform,false);
            var tr=go.GetComponent<RectTransform>();tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=Vector2.zero;tr.offsetMax=Vector2.zero;
            var txt=go.GetComponent<Text>();txt.font=font;txt.fontSize=fontSize;txt.color=tint;
            txt.alignment=TextAnchor.MiddleCenter;txt.horizontalOverflow=HorizontalWrapMode.Wrap;
            txt.verticalOverflow=VerticalWrapMode.Truncate;txt.text=words;
            txt.resizeTextForBestFit=true;txt.resizeTextMinSize=Mathf.Max(12,fontSize-12);txt.resizeTextMaxSize=fontSize;
        }
    }
}
