using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// V5: four progressively harder 2D levels, physically editable in Unity.
public sealed class BublikGame : MonoBehaviour
{
    public Camera Camera2D;
    public BublikPlayer Player;
    public Transform Bublik;
    public SpriteRenderer BublikPicture;
    public Sprite[] DogSprites;
    public GameObject Home;
    public GameObject[] Levels;
    public SpriteRenderer MenuBackdrop, HomeBackdrop;
    public SpriteRenderer[] LevelBackdrops;
    public SpriteRenderer BiteRift;
    public Transform HomeDogDestination;
    public Transform[] StageExits;
    public Transform[] ClockObjects;
    public BublikDoor[] Doors;
    public BublikCollectible[] Collectibles;
    public BublikMovingPlatform[] Movers;
    public BublikTimeGate[] TimeGates;
    public Sprite PortraitBoy, PortraitDog, PortraitMother, ButtonArt;

    enum Phase { Menu, Intro, Home, Bite, Shock, Play, Interlude, Ending, Finished }
    Phase phase=Phase.Menu;
    int stage, dialogueIndex, total, collected;
    bool dogCalled, clockFrozen, paused;
    float dogHomeX, biteAt, shake, toastTo, nextAllowed;
    string toast="";
    Vector3[] itemOrigins;
    float[] levelStarts={15.6f,62f,116f,168f};
    float[] levelEnds={61.6f,110f,177.2f,220.2f};
    float[] checkpoints={17f,63f,117f,169f};
    readonly string[] openingSpeakers={"МАМА", "САША", "МАМА", "САША", "БУБЛИК"};
    readonly string[] openingLines={
        "Саша, погуляй с Бубликом. И пожалуйста, следи за ним: он снова всё грызёт.",
        "Мам, это просто пёс. Что самое страшное он может сгрызть?",
        "В прошлый раз он съел твои наушники. Я предупредила.",
        "Пойдём, Бублик. Сегодня просто хорошая прогулка, ладно?",
        "Гав!"
    };
    readonly string[] shockSpeakers={"САША","САША","САША","САША"};
    readonly string[] shockLines={
        "БУБЛИК?! ТЫ ЗАЧЕМ УКУСИЛ ВОЗДУХ?!",
        "ЧТО?! Стой! Где наш дом?! Почему дома ЛЕТАЮТ?!",
        "Нет, нет, нет! Этого быть не может! Это вообще как чинится?!",
        "Так. Соберись, Саша. Найду осколки, поймаю Бублика и верну всё обратно!"
    };
    readonly string[] interludeSpeakers={"САША","САША","САША"};
    readonly string[] interludeLines={
        "Гравитация почти на месте... А Бублик уже скрылся в дверях. Нельзя его упускать!",
        "Эти двери меня чуть с ума не свели. Только бы теперь часы работали нормально...",
        "Со временем разобрался. А теперь — последняя погоня. Бублик, стой!"
    };
    readonly string[] endSpeakers={"САША","САША","БУБЛИК","САША","САША"};
    readonly string[] endLines={
        "Ну наконец-то. Бублик! Я весь день бегаю по кусочкам неба из-за тебя!",
        "Обычные собаки грызут тапки. Иногда диван. Ты умудрился сгрызть пространство и время.",
        "Гав!",
        "Не смотри на меня так... Ладно. Главное, что ты цел. Давай домой.",
        "Такая вот фигня, собачка."
    };
    readonly string[] objectives={
        "Собери четыре осколка и найди Бублика",
        "Найди путь через двери и собери три осколка",
        "Останови время, перейди по мостам и собери три осколка",
        "Используй двери и часы, собери четыре осколка"
    };
    Texture2D blank;
    [System.NonSerialized]
    GUIStyle textStyle,titleStyle,menuTitle,menuSub,buttonText,speakerStyle,bodyStyle,smallStyle,objectiveStyle,objectiveShadow,centered;
    AudioSource audioSource;
    AudioClip[] clips;

    public bool AllowMovement { get { return phase==Phase.Home || (phase==Phase.Play&&!paused); } }
    public bool InLowGravity { get { return phase==Phase.Play && (stage==0 || stage==3); } }
    void Awake()
    {
        if(!Camera2D)Camera2D=Camera.main;
        if(Player)Player.Game=this;
        blank=new Texture2D(1,1,TextureFormat.RGBA32,false);
        blank.SetPixel(0,0,Color.white);blank.Apply();
        audioSource=gameObject.AddComponent<AudioSource>();audioSource.volume=.20f;
        clips=new AudioClip[]{Tone(290,.15f),Tone(800,.11f),Tone(90,.43f),Tone(490,.23f)};
        RestartWithoutReload();
    }
    void RestartWithoutReload()
    {
        phase=Phase.Menu;stage=0;collected=0;dogCalled=false;clockFrozen=false;paused=false;
        dialogueIndex=0;dogHomeX=4.9f;toast="";toastTo=0;
        if(Player)Player.transform.localScale=Vector3.one;
        if(Bublik)Bublik.localScale=Vector3.one;
        if(MenuBackdrop)MenuBackdrop.gameObject.SetActive(true);
        if(HomeBackdrop)HomeBackdrop.gameObject.SetActive(false);
        if(Home)Home.SetActive(false);
        for(int i=0;i<Levels.Length;i++)if(Levels[i])Levels[i].SetActive(false);
        foreach(var sr in LevelBackdrops)if(sr)sr.gameObject.SetActive(false);
        if(BiteRift)BiteRift.gameObject.SetActive(false);
        if(Camera2D)Camera2D.transform.position=new Vector3(5f,0,-20f);
        if(BublikPicture)BublikPicture.enabled=false;
        if(Player){Player.Teleport(1f,-3f);Player.Visual.enabled=false;Player.Body.simulated=false;}
    }
    void StartGame()
    {
        phase=Phase.Intro;dialogueIndex=0;nextAllowed=Time.time+.25f;
        MenuBackdrop.gameObject.SetActive(false);HomeBackdrop.gameObject.SetActive(true);Home.SetActive(true);
        // Larger characters in the room so their scale matches the furniture.
        Player.transform.localScale=Vector3.one*1.38f;
        Bublik.localScale=Vector3.one*1.31f;
        Player.Body.simulated=true;Player.Visual.enabled=true;Player.Teleport(1.0f,-3f);
        BublikPicture.enabled=true;dogHomeX=4.9f;Bublik.position=new Vector3(dogHomeX,-3.0f,0);
        SnapCamera(5f);Play(1);
    }
    void Update()
    {
        if(!Player||!Camera2D)return;
        // Handle mouse clicks in Update, not through overlaying IMGUI controls.
        Vector2 mouse;
        bool click=BublikInput.PointerDown(out mouse);
        float mx=mouse.x*1600f/Mathf.Max(1f,Screen.width);
        float my=(Screen.height-mouse.y)*900f/Mathf.Max(1f,Screen.height);
        bool next=(BublikInput.NextDown() || (click && my>560f && my<885f)) && Time.time>nextAllowed;
        if(phase==Phase.Menu)
        {
            if(click && mx>215f && mx<750f && my>390f && my<537f) { StartGame(); return; }
            if(click && mx>215f && mx<750f && my>=537f && my<665f) { Exit(); return; }
            if(BublikInput.NextDown())StartGame();
            return;
        }
        if(phase==Phase.Intro)
        {
            if(next){dialogueIndex++;nextAllowed=Time.time+.18f;Play(1);
                if(dialogueIndex>=openingLines.Length)phase=Phase.Home;}
        }
        else if(phase==Phase.Home)
        {
            if(!dogCalled&&BublikInput.InteractDown()&&Mathf.Abs(Player.transform.position.x-Bublik.position.x)<1.6f)
            {dogCalled=true;Toast("Ну всё, пошли... Только ничего не грызи!",2.6f);Play(1);}
            if(dogCalled)dogHomeX=Mathf.MoveTowards(dogHomeX,11.65f,Time.deltaTime*2.35f);
            Bublik.position=new Vector3(dogHomeX,-3.0f+.035f*Mathf.Sin(Time.time*8),0);
            BublikPicture.sprite=dogCalled?DogSprites[Mathf.FloorToInt(Time.time*10)%2+1]:DogSprites[0];
            Vector3 hp=Player.transform.position;
            // Screen-edge bounds rather than an invisible barrier in the middle.
            hp.x=Mathf.Clamp(hp.x,-2.92f,12.75f);
            Player.transform.position=hp;
            if(Player.Body)
            {
                Vector2 hv=Player.Body.linearVelocity;
                if((hp.x<=-2.91f&&hv.x<0)||(hp.x>=12.74f&&hv.x>0))hv.x=0;
                Player.Body.linearVelocity=hv;
            }
            if(dogCalled&&dogHomeX>11.45f&&Player.transform.position.x>10.05f)StartBite();
        }
        else if(phase==Phase.Bite)
        {
            float t=Time.time-biteAt;
            if(BiteRift)
            {
                BiteRift.gameObject.SetActive(true);
                BiteRift.transform.localScale=Vector3.one*Mathf.Lerp(.15f,1.28f,Mathf.Clamp01(t/.85f));
                BiteRift.transform.Rotate(0,0,Time.deltaTime*15);
            }
            if(t>1.45f){if(BiteRift)BiteRift.gameObject.SetActive(false);EnterStage(0);phase=Phase.Shock;dialogueIndex=0;nextAllowed=Time.time+.25f;Play(2);}
        }
        else if(phase==Phase.Shock)
        {
            if(next){dialogueIndex++;nextAllowed=Time.time+.19f;
                if(dialogueIndex>=shockLines.Length)phase=Phase.Play;}
        }
        else if(phase==Phase.Play)
        {
            if(BublikInput.EscapeDown())paused=!paused;
            if(!paused)
            {
                CollectNearby(); UpdateCheckpoint();
                if(BublikInput.InteractDown())Interact();
            }
        }
        else if(phase==Phase.Interlude)
        {
            if(next){nextAllowed=Time.time+.20f;EnterStage(stage+1);phase=Phase.Play;}
        }
        else if(phase==Phase.Ending)
        {
            if(next){dialogueIndex++;nextAllowed=Time.time+.20f;
                if(dialogueIndex>=endLines.Length){phase=Phase.Finished;Play(3);}}
        }
        else if(phase==Phase.Finished)
        {
            if(click && mx>520f && mx<1110f && my>540f && my<657f){RestartWithoutReload();return;}
            if(click && mx>520f && mx<1110f && my>=657f && my<782f){Exit();return;}
            if(BublikInput.NextDown())RestartWithoutReload();
        }
        if(phase!=Phase.Menu && phase!=Phase.Finished)
        {
            FollowCamera();
            if(phase==Phase.Play||phase==Phase.Interlude||phase==Phase.Ending)
            {
                if(BublikPicture){
                    BublikPicture.enabled=true;
                    BublikPicture.sprite=(phase==Phase.Play && Mathf.FloorToInt(Time.time*2)%8==0)?DogSprites[0]:DogSprites[3];
                    if(StageExits.Length>stage&&StageExits[stage])Bublik.position=StageExits[stage].position;
                }
            }
        }
    }
    void StartBite()
    {
        phase=Phase.Bite;biteAt=Time.time;shake=.67f;Play(2);
        if(BiteRift){BiteRift.transform.position=new Vector3(11.8f,-1.5f,0);BiteRift.gameObject.SetActive(true);}
        if(BublikPicture)BublikPicture.sprite=DogSprites[4];
    }
    void EnterStage(int index)
    {
        stage=index;collected=0;clockFrozen=false;paused=false;
        if(Player)Player.transform.localScale=Vector3.one;
        if(Bublik)Bublik.localScale=Vector3.one;
        if(Home)Home.SetActive(false);
        if(HomeBackdrop)HomeBackdrop.gameObject.SetActive(false);
        for(int i=0;i<Levels.Length;i++)if(Levels[i])Levels[i].SetActive(i==stage);
        for(int i=0;i<LevelBackdrops.Length;i++)if(LevelBackdrops[i])LevelBackdrops[i].gameObject.SetActive(i==stage);
        foreach(var shard in Collectibles)if(shard&&shard.Stage==stage)
        {shard.Taken=false;shard.gameObject.SetActive(true);}
        foreach(var m in Movers)if(m&&m.Stage==stage)m.Unfreeze();
        if(TimeGates!=null) foreach(var gate in TimeGates) if(gate&&gate.Stage==stage)gate.SetOpen(false);
        total=0;
        foreach(var shard in Collectibles)if(shard&&shard.Stage==stage)total++;
        checkpoints[stage]=levelStarts[stage]+1.25f;
        Player.Teleport(checkpoints[stage],-3f);
        Bublik.position=StageExits[stage].position;
        SnapCamera(Player.transform.position.x+2.5f);
        Play(1);
    }
    void CollectNearby()
    {
        foreach(var shard in Collectibles)
        {
            if(!shard||!shard.gameObject.activeInHierarchy||shard.Stage!=stage||shard.Taken)continue;
            float dist=Vector2.Distance((Vector2)Player.transform.position+Vector2.up*.78f,(Vector2)shard.transform.position);
            if(dist<1f){shard.Taken=true;shard.gameObject.SetActive(false);collected++;Play(1);
                if(collected==total)Toast("Получилось! Теперь к Бублику!",2.7f);}
        }
    }
    void UpdateCheckpoint()
    {
        float x=Player.transform.position.x;
        if(stage==0){if(x>=42f)checkpoints[stage]=41f;else if(x>=31f)checkpoints[stage]=30.6f;}
        if(stage==1){if(x>=99f)checkpoints[stage]=99f;else if(x>=82f)checkpoints[stage]=82.4f;}
        if(stage==2){if(x>=164f)checkpoints[stage]=166f;else if(x>=121f)checkpoints[stage]=120.1f;}
        if(stage==3){if(x>=204f)checkpoints[stage]=203f;else if(x>=188f)checkpoints[stage]=188.0f;else if(x>=178f)checkpoints[stage]=184.4f;}
    }
    void Interact()
    {
        if(stage==1||stage==3)
        {
            BublikDoor d=NearestDoor(1.7f);
            if(d)
            {
                if(d.Exit)TryEndStage();
                else {Player.Teleport(d.TargetX,d.TargetGround);SnapCamera(Player.transform.position.x+2f);
                     Toast(d.Wrong?"Эй! Эта дверь опять вернула меня назад!":"Ого... Эта дверь куда-то ведёт!",2f);Play(d.Wrong?0:3);}
                return;
            }
        }
        if((stage==2||stage==3) && ClockObjects!=null && stage<ClockObjects.Length && ClockObjects[stage] && Mathf.Abs(Player.transform.position.x-ClockObjects[stage].position.x)<1.75f)
        {
            bool frozen=ToggleTime();
            Toast(frozen?"Тик! Время остановлено. Мост теперь твёрдый!":"Тик-так! Время снова идёт. Мосты движутся!",3.0f);
            return;
        }
        if(StageExits!=null&&StageExits.Length>stage&&StageExits[stage]&&
            Vector2.Distance(Player.transform.position,StageExits[stage].position)<1.75f)
            TryEndStage();
    }
    bool ToggleTime()
    {
        clockFrozen=!clockFrozen;
        foreach(var m in Movers)if(m&&m.Stage==stage)
        {
            if(clockFrozen)m.TryFreeze();
            else m.Unfreeze();
        }
        if(TimeGates!=null) foreach(var gate in TimeGates)
            if(gate&&gate.Stage==stage)gate.SetOpen(clockFrozen);
        Play(2);
        return clockFrozen;
    }
    BublikDoor NearestDoor(float reach)
    {
        BublikDoor result=null;float nearest=reach;
        foreach(var d in Doors)
        {
            if(!d || d.Stage!=stage || !d.gameObject.activeInHierarchy)continue;
            float dist=Mathf.Abs(Player.transform.position.x-d.transform.position.x);
            if(dist<nearest){nearest=dist;result=d;}
        }
        return result;
    }
    void TryEndStage()
    {
        if(collected<total){Toast("Нужно собрать оставшиеся осколки: "+(total-collected),2.7f);return;}
        if((stage==2||stage==3)&&!clockFrozen){Toast("Сперва нужно остановить время.",2.6f);return;}
        Play(3);
        if(stage==3){phase=Phase.Ending;dialogueIndex=0;nextAllowed=Time.time+.25f;}
        else {phase=Phase.Interlude;nextAllowed=Time.time+.25f;}
    }
    public void Respawn()
    {
        if(phase!=Phase.Play&&phase!=Phase.Home)return;
        if(phase==Phase.Home)Player.Teleport(1f,-3f);
        else Player.Teleport(checkpoints[stage],-3f);
        SnapCamera(Player.transform.position.x+2.5f);
        Toast("Ай! Надо аккуратнее...",1.9f);Play(0);
    }
    void FollowCamera()
    {
        if(phase==Phase.Intro||phase==Phase.Home||phase==Phase.Bite) {SnapCamera(5f);return;}
        // Keep following through the final platform and portal.
        if(stage==3){MoveCam(Player.transform.position.x+2.8f);return;}
        float span=Camera2D.orthographicSize*Camera2D.aspect;
        float minX=levelStarts[stage]+span-.3f,maxX=levelEnds[stage]-span+.3f;
        float target=Mathf.Clamp(Player.transform.position.x+2.8f,minX,maxX);
        MoveCam(target);
    }
    void MoveCam(float targetX)
    {
        Vector3 desired=new Vector3(targetX,0,-20f);
        if(shake>0){desired.x+=UnityEngine.Random.Range(-shake,shake)*.3f;desired.y+=UnityEngine.Random.Range(-shake,shake)*.2f;shake=Mathf.Max(0,shake-Time.deltaTime*.7f);}
        Camera2D.transform.position=Vector3.Lerp(Camera2D.transform.position,desired,Time.deltaTime*5.5f);
    }
    void SnapCamera(float x){Camera2D.transform.position=new Vector3(x,0,-20f);}
    void Toast(string s,float duration){toast=s;toastTo=Time.time+duration;}
    AudioClip Tone(float hz,float seconds)
    {
        int sample=22050,amount=(int)(sample*seconds);float[] data=new float[amount];
        for(int i=0;i<amount;i++){float time=(float)i/sample;data[i]=Mathf.Sin(time*hz*6.28318f)*.17f*Mathf.Pow(1-(float)i/amount,2);}
        AudioClip clip=AudioClip.Create("Bublik_"+hz,amount,1,sample,false);clip.SetData(data,0);return clip;
    }
    void Play(int n){if(audioSource&&clips!=null&&n>=0&&n<clips.Length)audioSource.PlayOneShot(clips[n]);}
    public void PlayJump(){Play(1);}
    void Exit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying=false;
#else
        Application.Quit();
#endif
    }
    void InitGUI()
    {
        if(textStyle!=null && textStyle.fontSize==32 && objectiveStyle!=null && objectiveStyle.fontSize==34)return;
        textStyle=new GUIStyle(GUI.skin.label);textStyle.fontSize=32;textStyle.normal.textColor=Color.white;textStyle.wordWrap=true;
        titleStyle=new GUIStyle(textStyle);titleStyle.fontSize=54;titleStyle.fontStyle=FontStyle.Bold;
        menuTitle=new GUIStyle(titleStyle);menuTitle.fontSize=71;menuTitle.alignment=TextAnchor.MiddleCenter;
        menuSub=new GUIStyle(menuTitle);menuSub.fontSize=59;menuSub.normal.textColor=new Color(1,.75f,.82f);
        buttonText=new GUIStyle(textStyle);buttonText.fontSize=34;buttonText.fontStyle=FontStyle.Bold;buttonText.alignment=TextAnchor.MiddleCenter;buttonText.normal.textColor=new Color(.20f,.14f,.25f);
        speakerStyle=new GUIStyle(GUI.skin.label);
        speakerStyle.fontSize=30; speakerStyle.fontStyle=FontStyle.Normal;
        speakerStyle.alignment=TextAnchor.MiddleLeft;
        speakerStyle.normal.textColor=new Color(1f,.82f,.46f,1f);
        speakerStyle.hover.textColor=speakerStyle.normal.textColor;
        speakerStyle.active.textColor=speakerStyle.normal.textColor;
        speakerStyle.focused.textColor=speakerStyle.normal.textColor;
        speakerStyle.onNormal.textColor=speakerStyle.normal.textColor;
        speakerStyle.onHover.textColor=speakerStyle.normal.textColor;
        speakerStyle.onActive.textColor=speakerStyle.normal.textColor;
        speakerStyle.onFocused.textColor=speakerStyle.normal.textColor;
        bodyStyle=new GUIStyle(textStyle);bodyStyle.fontSize=34;bodyStyle.wordWrap=true;
        bodyStyle.normal.textColor=Color.white;bodyStyle.alignment=TextAnchor.MiddleLeft;
        bodyStyle.hover.textColor=bodyStyle.normal.textColor;
        bodyStyle.active.textColor=bodyStyle.normal.textColor;
        bodyStyle.focused.textColor=bodyStyle.normal.textColor;
        smallStyle=new GUIStyle(textStyle);smallStyle.fontSize=28;
        objectiveStyle=new GUIStyle(textStyle);objectiveStyle.fontSize=34;objectiveStyle.fontStyle=FontStyle.Bold;
        objectiveShadow=new GUIStyle(objectiveStyle);objectiveShadow.normal.textColor=new Color(.21f,.16f,.30f,.95f);
        centered=new GUIStyle(textStyle);centered.alignment=TextAnchor.MiddleCenter;
    }
    void Fill(Rect r,Color c){Color old=GUI.color;GUI.color=c;GUI.DrawTexture(r,blank);GUI.color=old;}
    void Label(Rect r,string s,GUIStyle sty){GUI.Label(r,s,sty);}
    void Portrait(Sprite s,Rect r)
    {
        if(!s)return;
        Rect tx=new Rect(s.rect.x/s.texture.width,s.rect.y/s.texture.height,s.rect.width/s.texture.width,s.rect.height/s.texture.height);
        GUI.DrawTextureWithTexCoords(r,s.texture,tx,true);
    }
    Sprite SpeakerPic(string speaker){return speaker=="МАМА"?PortraitMother:speaker=="БУБЛИК"?PortraitDog:PortraitBoy;}
    void Dialogue(string speaker,string line)
    {
        // Passive text + portrait only. Mouse/keyboard advance in Update().
        Fill(new Rect(67,600,1466,251),new Color(.21f,.15f,.31f,.95f));
        Fill(new Rect(75,604,1450,239),new Color(.15f,.10f,.23f,1f));
        Portrait(SpeakerPic(speaker),new Rect(112,658,145,157));
        Label(new Rect(292,619,1150,42),speaker,speakerStyle);
        Label(new Rect(292,670,1190,155),line,bodyStyle);
        Label(new Rect(1462,797,50,35),"›",speakerStyle);
    }
    void FinishButton(Rect r,string label)
    {
        if(ButtonArt)GUI.DrawTexture(r,ButtonArt.texture,ScaleMode.StretchToFill,true);
        GUI.Label(r,label,buttonText);
    }
    void ObjectivePanel(Rect r)
    {
        Fill(new Rect(r.x+4,r.y+5,r.width,r.height),new Color(.11f,.08f,.19f,.36f));
        Fill(r,new Color(.17f,.11f,.26f,.72f));
        Fill(new Rect(r.x+3,r.y+3,r.width-6,r.height-6),new Color(.27f,.20f,.42f,.42f));
    }
    void HomeObjective()
    {
        Rect box=new Rect(22,18,500,68);
        ObjectivePanel(box);
        GUIStyle label=new GUIStyle(objectiveStyle);
        label.fontSize=34;
        label.normal.textColor=new Color(.98f,.96f,.90f,1f);
        label.alignment=TextAnchor.MiddleLeft;
        GUI.Label(new Rect(box.x+16,box.y+5,box.width-24,box.height-8),"Погулять с Бубликом",label);
    }
    void Objective()
    {
        Rect box=new Rect(22,18,900,100);
        ObjectivePanel(box);
        string msg=objectives[stage]+"  ("+collected+"/"+total+")";
        Label(new Rect(box.x+18,box.y+8,box.width-24,box.height-10),msg,objectiveStyle);
    }
    void TinyNotice()
    {
        if(Time.time>toastTo)return;
        Fill(new Rect(345,786,910,66),new Color(.21f,.14f,.29f,.77f));
        Label(new Rect(365,795,865,48),toast,centered);
    }
    void WorldE(Transform p)
    {
        if(!p)return;
        Vector3 screen=Camera2D.WorldToScreenPoint(p.position+Vector3.up*1.7f);
        if(screen.z<0)return;
        float x=screen.x*1600f/Screen.width;
        float y=(Screen.height-screen.y)*900f/Screen.height;
        if(x<35||x>1565||y<80||y>845)return;
        Fill(new Rect(x-27,y-28,54,50),new Color(.26f,.18f,.41f,.94f));
        Label(new Rect(x-27,y-30,54,50),"E",new GUIStyle(centered){fontSize=29,fontStyle=FontStyle.Bold});
    }
    void ShowNearbyInteraction()
    {
        float px=Player.transform.position.x;
        if(phase==Phase.Home && !dogCalled&&Mathf.Abs(px-Bublik.position.x)<1.8f){WorldE(Bublik);return;}
        if(phase!=Phase.Play)return;
        var door=NearestDoor(1.9f);if(door!=null){WorldE(door.transform);return;}
        if((stage==2||stage==3)&&ClockObjects[stage]&&Mathf.Abs(px-ClockObjects[stage].position.x)<1.8f){WorldE(ClockObjects[stage]);return;}
        if(StageExits[stage]&&Vector2.Distance(Player.transform.position,StageExits[stage].position)<1.9f)WorldE(StageExits[stage]);
    }
    void OnGUI()
    {
        if(blank==null)return;
        InitGUI();Matrix4x4 previous=GUI.matrix;
        GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1600f,Screen.height/900f,1));
        if(phase==Phase.Menu)
        {
            // The HD menu artwork already contains the title and both button visuals.
            // We only place invisible click areas over the painted buttons.
            // Click regions are processed in Update; render nothing over painted buttons.
        }
        else if(phase==Phase.Intro)Dialogue(openingSpeakers[Mathf.Min(dialogueIndex,openingLines.Length-1)],openingLines[Mathf.Min(dialogueIndex,openingLines.Length-1)]);
        else if(phase==Phase.Home){HomeObjective();ShowNearbyInteraction();TinyNotice();}
        else if(phase==Phase.Bite)
        {Label(new Rect(600,230,500,130),"ХРУМ!",new GUIStyle(titleStyle){fontSize=87,alignment=TextAnchor.MiddleCenter});}
        else if(phase==Phase.Shock)Dialogue(shockSpeakers[Mathf.Min(dialogueIndex,shockLines.Length-1)],shockLines[Mathf.Min(dialogueIndex,shockLines.Length-1)]);
        else if(phase==Phase.Play){Objective();ShowNearbyInteraction();TinyNotice();if(paused){Fill(new Rect(0,0,1600,900),new Color(.13f,.1f,.20f,.58f));Label(new Rect(540,325,520,105),"ПАУЗА",menuTitle);}}
        else if(phase==Phase.Interlude)Dialogue(interludeSpeakers[Mathf.Min(stage,interludeLines.Length-1)],interludeLines[Mathf.Min(stage,interludeLines.Length-1)]);
        else if(phase==Phase.Ending)Dialogue(endSpeakers[Mathf.Min(dialogueIndex,endLines.Length-1)],endLines[Mathf.Min(dialogueIndex,endLines.Length-1)]);
        else if(phase==Phase.Finished)
        {
            Fill(new Rect(0,0,1600,900),new Color(.14f,.08f,.25f,.6f));
            Label(new Rect(185,210,1240,180),"ТАКАЯ ВОТ ФИГНЯ, СОБАЧКА.",new GUIStyle(menuTitle){fontSize=59});
            Portrait(PortraitDog,new Rect(693,357,212,175));
            FinishButton(new Rect(589,555,422,90),"СЫГРАТЬ ЕЩЁ");
            FinishButton(new Rect(589,663,422,88),"ВЫЙТИ");
        }
        GUI.matrix=previous;
    }
}
