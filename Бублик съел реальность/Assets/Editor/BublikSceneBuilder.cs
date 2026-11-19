#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Creates a conventional editable 2D .unity scene. Rebuilding is explicit after the first import.
[InitializeOnLoad]
public static class BublikSceneBuilder
{
    const string ScenePath="Assets/Scenes/BublikMain.unity";
    const string ArtPath="Assets/Resources/Art/";
    static readonly Dictionary<string,Sprite> textures=new Dictionary<string,Sprite>();
    static readonly List<BublikCollectible> gems=new List<BublikCollectible>();
    static readonly List<BublikDoor> doors=new List<BublikDoor>();
    static readonly List<BublikMovingPlatform> platforms=new List<BublikMovingPlatform>();
    static readonly List<BublikTimeGate> timeGates=new List<BublikTimeGate>();
    static readonly List<GameObject> levels=new List<GameObject>();
    static readonly List<SpriteRenderer> bg=new List<SpriteRenderer>();
    static readonly List<Transform> exits=new List<Transform>();
    static readonly Transform[] clocks=new Transform[4];

    static BublikSceneBuilder(){EditorApplication.delayCall+=AutoCreate;}
    static void AutoCreate()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(!File.Exists(ScenePath))CreateScene();
    }
    [MenuItem("Бублик V6.5/Открыть игровую сцену")]
    public static void OpenScene()
    {
        if(!File.Exists(ScenePath)){CreateScene();return;}
        EditorSceneManager.OpenScene(ScenePath);Force2D();
    }
    [MenuItem("Бублик V6.5/Пересоздать сцену из ассетов")]
    public static void Rebuild()
    {
        if(EditorUtility.DisplayDialog("Пересоздать сцену?","BublikMain.unity будет перезаписана. Свои изменения сначала сохрани в копию сцены.","Пересоздать","Отмена"))CreateScene();
    }
    static void Force2D(){foreach(SceneView view in SceneView.sceneViews)view.in2DMode=true;}
    static Sprite Load(string name)
    {
        Sprite ready;
        if(textures.TryGetValue(name,out ready)&&ready!=null)return ready;
        string path=ArtPath+name+".png";
        TextureImporter importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if(importer!=null)
        {
            if(importer.textureType!=TextureImporterType.Sprite || importer.mipmapEnabled || importer.spriteImportMode!=SpriteImportMode.Single)
            {
                importer.textureType=TextureImporterType.Sprite;
                importer.spriteImportMode=SpriteImportMode.Single;
                importer.alphaIsTransparency=true;
                importer.mipmapEnabled=false;
                importer.SaveAndReimport();
            }
        }
        ready=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if(!ready)Debug.LogError("V5 art missing: "+path);
        textures[name]=ready;
        return ready;
    }
    static GameObject G(string name,Transform parent=null,float x=0,float y=0)
    {
        GameObject go=new GameObject(name);
        if(parent)go.transform.SetParent(parent,false);
        go.transform.localPosition=new Vector3(x,y,0);
        return go;
    }
    static void Scale(SpriteRenderer r,float w,float h)
    {
        if(!r.sprite)return;
        Vector2 size=r.sprite.bounds.size;
        r.transform.localScale=new Vector3(w/size.x,h/size.y,1);
    }
    static SpriteRenderer Art(Transform parent,string name,string file,float x,float y,float w,float h,int sorting)
    {
        GameObject go=G(name,parent,x,y);
        var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=Load(file);renderer.sortingOrder=sorting;
        Scale(renderer,w,h);return renderer;
    }
    static BoxCollider2D Hit(Transform parent,string name,float x,float y,float width,float height)
    {
        GameObject g=G(name,parent,x,y);g.layer=8;
        var c=g.AddComponent<BoxCollider2D>();
        c.size=new Vector2(width,height);
        c.edgeRadius=.06f;
        return c;
    }
    // The image's top (NOT its bottom) is the physical top of the collider.
    static void Island(Transform parent,string name,float x,float top,float width)
    {
        Art(parent,name+" / CLEAN ART","platform_clean",x,top-.36f,width,.72f,12);
        Hit(parent,name+" / SOLID 2D",x,top-.09f,width-.16f,.18f);
    }
    static void Ground(Transform parent,string name,float l,float r,float top=-3f)
    {
        int count=Mathf.CeilToInt((r-l)/4f);
        float segment=(r-l)/count;
        for(int i=0;i<count;i++)
            Art(parent,name+" / tile "+i,"platform_clean",l+segment*(i+.5f),top-.36f,segment+.025f,.72f,11);
        Hit(parent,name+" / one smooth 2D collider",(l+r)*.5f,top-.09f,r-l,.18f);
    }
    static BublikCollectible Shard(Transform parent,int stage,float x,float y)
    {
        int variation=gems.Count%3;
        float[] widths={.88f,1.02f,.94f};
        float[] heights={1.18f,1.34f,1.24f};
        Color[] tints={Color.white,new Color(.82f,.94f,1f,1f),new Color(1f,.88f,.98f,1f)};
        SpriteRenderer r=Art(parent,"COLLECTIBLE " + stage + " (large crystal)","crystal_clean",x,y,widths[variation],heights[variation],25);
        r.color=tints[variation];
        var s=r.gameObject.AddComponent<BublikCollectible>();s.Stage=stage;s.BobPhase=gems.Count*1.37f;s.RestPosition=r.transform.position;gems.Add(s);return s;
    }
    static BublikDoor Door(Transform parent,int stage,string name,float x,float top,string art,float targetX,bool isWrong=false,bool exit=false)
    {
        var sr=Art(parent,name,art,x,top+1.09f,1.27f,2.18f,17);
        var d=sr.gameObject.AddComponent<BublikDoor>();d.Stage=stage;d.TargetX=targetX;d.TargetGround=-3f;d.Wrong=isWrong;d.Exit=exit;doors.Add(d);
        return d;
    }
    static BublikMovingPlatform Moving(Transform parent,int stage,string name,float x,float top,float w,float amp,float speed,float phase)
    {
        GameObject g=G(name,parent,x,top);g.layer=8;
        Rigidbody2D rb=g.AddComponent<Rigidbody2D>();rb.bodyType=RigidbodyType2D.Kinematic;rb.gravityScale=0;
        BoxCollider2D c=g.AddComponent<BoxCollider2D>();c.offset=new Vector2(0,-.10f);c.size=new Vector2(w-.16f,.20f);c.edgeRadius=.06f;
        Art(g.transform,"Animated visual + aligned physical surface","platform_clean",0,-.36f,w,.72f,15);
        var mover=g.AddComponent<BublikMovingPlatform>();mover.Stage=stage;mover.BaseX=x;mover.BaseY=top;mover.IdealX=x;
        mover.Amplitude=amp;mover.Speed=speed;mover.Phase=phase;
        platforms.Add(mover);return mover;
    }
    static void TimeGate(Transform parent,int stage,string name,float x)
    {
        var go=G(name,parent,x,.2f);
        var c=go.AddComponent<BoxCollider2D>();
        c.size=new Vector2(.42f,10f); c.offset=new Vector2(0,0);
        SpriteRenderer beam=Art(go.transform,"Visible locked time barrier","time_gate_beam",0,0,.67f,8.2f,29);
        var gate=go.AddComponent<BublikTimeGate>();gate.Stage=stage;gate.Visual=beam;
        timeGates.Add(gate);
    }
    static void Decor(Transform stage,float x,float y,string art="lantern_clean",float w=.60f,float h=1.02f)
    {
        Art(stage,"Soft cartoon decoration",art,x,y,w,h,5);
    }
    static Transform ExitAt(Transform parent,float x)
    {
        GameObject mark=G("BUBLIK EXIT — interact E only here",parent,x,-3f);
        exits.Add(mark.transform);
        // No automatic arrows: the visible dog and contextual E marker show the goal.
        return mark.transform;
    }
    static void LevelGravity(Transform level)
    {
        // Chapter 1: slightly longer, readable, and fully visible to the camera.
        Ground(level,"Starting grass",15.0f,23.0f);
        Ground(level,"Checkpoint grass",29.9f,33.8f);
        Ground(level,"Second checkpoint",40.1f,44.1f);
        Ground(level,"Third checkpoint",47.8f,50.8f);
        Ground(level,"Finish grass",53.0f,61.2f);
        Island(level,"Floating island A",24.4f,-1.60f,3.1f);
        Island(level,"High floating island B",28.0f,-.92f,3.0f);
        Island(level,"Stair island C",35.4f,-1.10f,3.55f);
        Island(level,"Bonus high island D",38.4f,-.40f,2.55f);
        Island(level,"Break island E",46.1f,-1.58f,3.2f);
        Island(level,"Break island F",49.0f,-.92f,2.8f);
        Island(level,"Finish helper island G",52.0f,-1.35f,2.7f);
        Shard(level,0,24.4f,-.66f); Shard(level,0,28.05f,.03f);
        Shard(level,0,38.35f,.46f); Shard(level,0,49.05f,.02f);
        Decor(level,18.7f,.16f);
        Decor(level,31.8f,1.48f);
        Art(level,"Floating park tree decoration","tree_sky",42.15f,1.40f,1.06f,1.18f,4);
        Art(level,"Floating lantern decoration","lantern_clean",45.45f,1.46f,.56f,.84f,4);
        Art(level,"Floating bin decoration","trash_sky",56.15f,1.30f,.76f,.86f,4);
        ExitAt(level,58.3f);
    }
    static void LevelSpace(Transform level)
    {
        Ground(level,"Door puzzle section A",61.7f,72.1f);
        Ground(level,"Door puzzle section B",81.0f,88.6f);
        Ground(level,"Door puzzle section C",98.3f,110.5f);
        // Door colours do not indicate which doors are safe: correct and trap doors are mixed.
        Door(level,1,"TEAL trap / sends you back",64.8f,-3f,"door_teal",63.1f,true);
        Door(level,1,"ROSE door / takes you to sector 2",68.0f,-3f,"door_rose",82.3f);
        Door(level,1,"GOLD trap / loops to the entrance",71.0f,-3f,"door_gold",63.1f,true);
        Door(level,1,"GOLD door / takes you to sector 3",82.5f,-3f,"door_gold",99.5f);
        Door(level,1,"TEAL trap / returns to sector 1",85.2f,-3f,"door_teal",63.1f,true);
        Door(level,1,"ROSE trap / loops inside sector 2",87.1f,-3f,"door_rose",82.3f,true);
        Door(level,1,"ROSE trap / sends you back",99.3f,-3f,"door_rose",82.3f,true);
        Door(level,1,"TEAL door / shortcut to the exit",102.2f,-3f,"door_teal",107.2f);
        Door(level,1,"GOLD trap / returns to the entrance",104.8f,-3f,"door_gold",63.1f,true);
        Door(level,1,"EXIT DOOR / opens after crystals",108.4f,-3f,"door_gold",0,false,true);
        Shard(level,1,68.5f,-1.38f);Shard(level,1,85.5f,-.72f);Shard(level,1,100.9f,-1.55f);
        Art(level,"Doorway hint","lantern_clean",68.0f,.55f,.49f,.78f,10);
        ExitAt(level,109.4f);
    }
    static void LevelTime(Transform level)
    {
        // A broad chasm makes the clock and all four moving bridges mandatory.
        // There are no fixed stepping stones that allow the player to bypass the mechanic.
        Ground(level,"Clock starting shore",115.3f,126.0f);
        Ground(level,"Far clock shore",164.5f,177.2f);
        TimeGate(level,2,"TIME SEAL - open the clock before crossing",127.0f);
        Moving(level,2,"TIME BRIDGE 1 - freeze to make it solid",133.0f,-2.00f,3.35f,2.1f,1.05f,0);
        Moving(level,2,"TIME BRIDGE 2 - freeze to make it solid",141.0f,-.75f,3.35f,2.0f,.95f,Mathf.PI*.6f);
        Moving(level,2,"TIME BRIDGE 3 - freeze to make it solid",149.0f,-1.65f,3.35f,2.1f,1.00f,Mathf.PI);
        Moving(level,2,"TIME BRIDGE 4 - freeze to make it solid",157.0f,-.55f,3.35f,1.8f,.90f,Mathf.PI*1.5f);
        var sr=Art(level,"GIANT CLOCK - stop the moving bridges","clock_clean",120.5f,-1.80f,1.75f,2.05f,20);
        clocks[2]=sr.transform;
        Shard(level,2,123.2f,-1.48f);Shard(level,2,141.0f,.32f);Shard(level,2,169.2f,-1.52f);
        Decor(level,117.4f,1f);
        Art(level,"Far shore tree","tree_sky",173.3f,1.05f,1.18f,1.30f,4);
        ExitAt(level,174.4f);
    }
    static void LevelFinal(Transform level)
    {
        // Door is mandatory: without it there is a huge impossible gap.
        Ground(level,"Start of combined world",167f,171.7f);
        Ground(level,"After first door",183.8f,188.8f);
        Ground(level,"After frozen bridge",200.7f,204.8f);
        Ground(level,"Last safe grass",211.3f,220.2f);
        Island(level,"High island one",179.1f,-1.55f,3.2f);
        Island(level,"High island two",191.8f,-1.45f,3.2f);
        Island(level,"Small island before clock",196.0f,-.95f,2.9f);
        Island(level,"Small island before finale",207.7f,-1.10f,3.05f);
        TimeGate(level,3,"BLOCKING TIME SEAL - requires clock",194.15f);
        Moving(level,3,"FINAL MOVING PLATFORM A",196.8f,-1.55f,3.2f,1.15f,1.45f,0);
        Moving(level,3,"FINAL MOVING PLATFORM B",199.5f,-0.95f,2.9f,.85f,1.25f,Mathf.PI*.5f);
        Door(level,3,"DOOR right - jumps a sector",170.0f,-3f,"door_teal",184.6f);
        Door(level,3,"DOOR wrong - sends you back",172.6f,-3f,"door_rose",168.1f,true);
        Door(level,3,"DOOR return - recover any missed shards",186.8f,-3f,"door_rose",170.0f,true);
        Door(level,3,"DOOR forward - optional shortcut after clock",202.4f,-3f,"door_teal",212.0f);
        SpriteRenderer r=Art(level,"TIME CLOCK NEXT TO BRIDGE","clock_clean",191.2f,-.66f,1.35f,1.62f,22);
        clocks[3]=r.transform;
        Shard(level,3,170.9f,-1.88f); Shard(level,3,179.2f,-.58f);
        Shard(level,3,191.7f,-.52f); Shard(level,3,207.9f,-.02f);
        Decor(level,174f,1.14f);
        Art(level,"Floating tree decoration","tree_sky",210.0f,1.16f,1.00f,1.16f,4);
        Art(level,"Floating bin decoration","trash_sky",198.7f,1.18f,.76f,.86f,4);
        Art(level,"Last clean MAGICAL portal","rift_clean",218.0f,-1.02f,2.28f,2.56f,5);
        ExitAt(level,216.0f);
    }
    public static void CreateScene()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        Directory.CreateDirectory("Assets/Scenes");
        gems.Clear();doors.Clear();platforms.Clear();timeGates.Clear();textures.Clear();levels.Clear();bg.Clear();exits.Clear();
        for(int i=0;i<clocks.Length;i++)clocks[i]=null;
        Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        BublikGame game=G("GAME LOGIC | V6 | fixed 2D puzzle stages").AddComponent<BublikGame>();
        GameObject cameraGo=G("2D CAMERA | ORTHOGRAPHIC");cameraGo.tag="MainCamera";
        Camera cam=cameraGo.AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=5.0f;
        cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.30f,.22f,.40f);
        cam.nearClipPlane=.01f;cam.farClipPlane=100f;
        cameraGo.transform.position=new Vector3(5f,0,-20);
        cameraGo.AddComponent<AudioListener>();game.Camera2D=cam;
        SpriteRenderer menu=Art(cameraGo.transform,"MENU ART / no gameplay objects","menu_pretty",0,0,17.78f,10.05f,-100);
        SpriteRenderer homeBG=Art(cameraGo.transform,"HOME ART / separate clean parallax","home_pretty",0,0,17.78f,10.05f,-100);
        menu.transform.localPosition=new Vector3(0,0,10);homeBG.transform.localPosition=new Vector3(0,0,10);
        homeBG.gameObject.SetActive(false);game.MenuBackdrop=menu;game.HomeBackdrop=homeBG;
        string[] files={"sky_gravity","sky_space","sky_time","sky_final"};
        for(int i=0;i<4;i++)
        {
            SpriteRenderer sceneBg=Art(cameraGo.transform,"CHAPTER BACKDROP / "+i,files[i],0,0,17.78f,10.05f,-100);
            sceneBg.transform.localPosition=new Vector3(0,0,10);sceneBg.gameObject.SetActive(false);bg.Add(sceneBg);
        }
        GameObject world=G("EDITABLE 2D WORLD");
        GameObject home=G("HOME / normal world",world.transform);game.Home=home;
        // One seamless floor and invisible boundary colliders at actual camera edges.
        Hit(home.transform,"Invisible living room floor",5.0f,-3.09f,17.1f,.22f);
        Hit(home.transform,"Invisible left room edge",-3.41f,-.55f,.20f,5.6f);
        Hit(home.transform,"Invisible right room edge",13.33f,-.55f,.20f,5.6f);
        var rift=Art(home.transform,"THE FIRST SPACE BITE (hidden)","rift_clean",11.8f,-1.5f,1.5f,1.7f,40);
        rift.gameObject.SetActive(false);game.BiteRift=rift;
        GameObject[] chapterObjects=new GameObject[4];
        for(int i=0;i<4;i++)
        {
            GameObject level=G("LEVEL "+(i+1)+" | clean floating islands | editable sprite colliders",world.transform);
            chapterObjects[i]=level;levels.Add(level);
            if(i==0)LevelGravity(level.transform);
            if(i==1)LevelSpace(level.transform);
            if(i==2)LevelTime(level.transform);
            if(i==3)LevelFinal(level.transform);
            level.SetActive(false);
        }
        game.Levels=chapterObjects;
        game.LevelBackdrops=bg.ToArray();
        GameObject player=G("SASHA / physics origin is EXACTLY under his feet",world.transform,1,-3);
        player.layer=0;
        Rigidbody2D rb=player.AddComponent<Rigidbody2D>();rb.gravityScale=1.75f;rb.freezeRotation=true;
        rb.collisionDetectionMode=CollisionDetectionMode2D.Continuous;rb.interpolation=RigidbodyInterpolation2D.Interpolate;
        BoxCollider2D hit=player.AddComponent<BoxCollider2D>();hit.size=new Vector2(.50f,1.34f);hit.offset=new Vector2(0,.67f);hit.edgeRadius=.05f;
        SpriteRenderer playerSprite=Art(player.transform,"SPRITE - shoe soles aligned to collider feet","hero_idle",0,.70f,.82f,1.50f,42);
        BublikPlayer hero=player.AddComponent<BublikPlayer>();hero.Game=game;hero.Body=rb;hero.Hitbox=hit;hero.Visual=playerSprite;
        hero.Idle=Load("hero_idle");hero.Run1=Load("hero_run1");hero.Run2=Load("hero_run2");hero.Jump=Load("hero_jump");hero.Fall=Load("hero_fall");
        hero.GroundMask=1<<8;game.Player=hero;
        GameObject dog=G("BUBLIK / genuine separate character sprite",world.transform,4.3f,-3);
        SpriteRenderer dogVisual=Art(dog.transform,"DOG LIVE SPRITE", "dog_idle",0,.46f,1.11f,.92f,43);
        game.Bublik=dog.transform;game.BublikPicture=dogVisual;
        game.DogSprites=new[]{Load("dog_idle"),Load("dog_run1"),Load("dog_run2"),Load("dog_sit"),Load("dog_bark")};
        game.StageExits=exits.ToArray();game.ClockObjects=clocks;game.Doors=doors.ToArray();
        game.Collectibles=gems.ToArray();game.Movers=platforms.ToArray();game.TimeGates=timeGates.ToArray();
        game.PortraitBoy=Load("hero_icon");game.PortraitDog=Load("dog_idle");game.PortraitMother=Load("portrait_mom");
        game.ButtonArt=Load("button_clean");
        EditorSceneManager.SaveScene(scene,ScenePath);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        AssetDatabase.SaveAssets();Force2D();EditorSceneManager.OpenScene(ScenePath);
        Selection.activeGameObject=player;
        Debug.Log("Бублик V6.5: добавлен фон у задачи, исправлен первый уровень и обновлены декоративные спрайты.");
    }
}
#endif
