using System;
using UnityEngine;
using UnityEngine.UI;

namespace GodniyGod
{
    public sealed class GGUI
    {
        public Font font;
        public Canvas canvas;
        public GameObject menu, instructions, hud, dialogue, report, seasonPacked, pauseMenu;
        private Text packedHeading;
        private Text packedActionLabel;
        private Action packedAction;
        private Text hudSeason, hudPage;
        public Button hudFinish, hudPrev, hudNext;
        private Text reportHeading, reportBody, reportNote, reportActionLabel, reportScore, reportVerdict;
        public Text dialogueSpeaker, dialogueText, dialogueNextText;
        private Button reportActionButton;
        private RectTransform hitLayer;
        private readonly RectTransform[] itemHitAreas = new RectTransform[GGWorld.ItemsPerPage];
        private readonly RectTransform[] boxHitAreas = new RectTransform[3];
        private Action onReportAction;
        private readonly GGGame game;
        private Sprite rounded;
        private readonly Color ink=new Color(.09f,.14f,.17f,1);
        private readonly Color white=new Color(.96f,.96f,.91f,1);
        private readonly Color cream=new Color(.98f,.87f,.62f,1);
        private readonly Color muted=new Color(.70f,.77f,.76f,1);
        private readonly Color panel=new Color(.10f,.16f,.19f,.97f);
        private readonly Color mint=new Color(.51f,.74f,.60f,1);
        public GGUI(GGGame owner){game=owner;}

        public void Build()
        {
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if(font==null)font=Font.CreateDynamicFontFromOSFont(new[]{"Arial", "Segoe UI", "DejaVu Sans"},38);
            rounded=MakeRoundedSprite();
            GameObject cg=new GameObject("ИНТЕРФЕЙС · ГОДНЫЙ ГОД",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas=cg.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=99;
            var scaler=cg.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;scaler.matchWidthOrHeight=.5f;
            BuildMenu();BuildHelp();BuildHud();BuildDialogue();BuildReport();BuildPacked();BuildPause();ShowMenu();
        }
        public void ShowMenu()
        {
            ClosePause();
            menu.SetActive(true);instructions.SetActive(false);hud.SetActive(false);dialogue.SetActive(false);report.SetActive(false);seasonPacked.SetActive(false);
            GGAnimation.Reveal(menu);
        }
        public void ShowPause()
        {
            if(pauseMenu==null)return;
            pauseMenu.SetActive(true);
            GGAnimation.Reveal(pauseMenu); // Uses SetUpdate(true): works at Time.timeScale=0.
        }
        public void ClosePause()
        {
            if(pauseMenu!=null)pauseMenu.SetActive(false);
        }
        private void BuildPause()
        {
            // Created last, so it draws on top of HUD, dialogue and reports.
            pauseMenu=FullScreen("ПАУЗА — внутриигровое меню");
            var shade=ImagePanel(pauseMenu.transform,"Затемнение паузы",Vector2.zero,Vector2.one,
                Vector2.zero,Vector2.zero,new Color(.025f,.045f,.062f,.90f),false);
            shade.GetComponent<Image>().raycastTarget=true; // Prevent clicks through overlay.
            var frame=ImagePanel(pauseMenu.transform,"Окно паузы",new Vector2(.33f,.28f),new Vector2(.67f,.74f),
                Vector2.zero,Vector2.zero,new Color(.08f,.15f,.19f,1f),true);
            Label(frame,"ПАУЗА",new Vector2(.10f,.70f),new Vector2(.90f,.92f),64,white,TextAnchor.MiddleCenter,true);
            MakeButton(frame,"ПРОДОЛЖИТЬ",new Vector2(.12f,.42f),new Vector2(.88f,.59f),
                new Color(.92f,.70f,.37f,1f),ink,31,()=>game.ResumeGame());
            MakeButton(frame,"ВЫЙТИ",new Vector2(.12f,.17f),new Vector2(.88f,.34f),
                new Color(.31f,.43f,.48f,1f),white,30,()=>game.ExitToMainMenu());
            Label(frame,"ESC — продолжить",new Vector2(.10f,.04f),new Vector2(.90f,.14f),
                19,muted,TextAnchor.MiddleCenter);
            pauseMenu.SetActive(false);
        }
        public void ShowHelp()
        {
            menu.SetActive(false);instructions.SetActive(true);hud.SetActive(false);dialogue.SetActive(false);report.SetActive(false);seasonPacked.SetActive(false);
        }
        public void ShowHud()
        {
            menu.SetActive(false);instructions.SetActive(false);hud.SetActive(true);dialogue.SetActive(false);report.SetActive(false);seasonPacked.SetActive(false);
        }
        public void ShowDialogue(string who,string message,bool last,bool ending)
        {
            menu.SetActive(false);instructions.SetActive(false);hud.SetActive(false);report.SetActive(false);seasonPacked.SetActive(false);dialogue.SetActive(true);
            dialogueSpeaker.text=who;dialogueText.text=message;
            GGAnimation.Reveal(dialogue);
            dialogueNextText.text=last?(ending?"К ИТОГАМ  →":"ПРИСТУПИТЬ  →"):"ДАЛЕЕ  →";
        }
        public void ShowReport(string title,string body,string note,string buttonText,Action action,string score="",string verdict="")
        {
            menu.SetActive(false);instructions.SetActive(false);hud.SetActive(false);dialogue.SetActive(false);seasonPacked.SetActive(false);report.SetActive(true);
            reportHeading.text=title;reportBody.text=body;
            reportScore.text=score;reportVerdict.text=verdict;
            reportNote.text=note;reportActionLabel.text=buttonText;onReportAction=action;
            GGAnimation.Reveal(report);
        }
        public void ShowSeasonPacked(int season,Action next)
        {
            menu.SetActive(false);instructions.SetActive(false);hud.SetActive(false);
            dialogue.SetActive(false);report.SetActive(false);seasonPacked.SetActive(true);
            string word=GGData.Seasons[season];
            packedHeading.text=word.Substring(0,1)+word.Substring(1).ToLowerInvariant()+(season==1?" упаковано":" упакована");
            packedActionLabel.text=season==3?"Отчёт за год":"Следующий сезон";
            packedAction=next;
            GGAnimation.Reveal(seasonPacked);
        }
        private void BuildPacked()
        {
            seasonPacked=FullScreen("Упаковка сезона — короткий экран");
            ImagePanel(seasonPacked.transform,"Непрозрачный фон",Vector2.zero,Vector2.one,
                Vector2.zero,Vector2.zero,new Color(.95f,.91f,.82f,1f),false);
            packedHeading=Label(seasonPacked.transform,"Весна упакована",new Vector2(.12f,.49f),
                new Vector2(.88f,.64f),48,ink,TextAnchor.MiddleCenter,true);
            var b=MakeButton(seasonPacked.transform,"Следующий сезон",
                new Vector2(.38f,.36f),new Vector2(.62f,.42f),ink,white,
                23,()=>packedAction?.Invoke(),false);
            packedActionLabel=b.GetComponentInChildren<Text>();
        }
        public void RefreshHud(int season,int delivered,int page,
            bool canFinish,bool busy,bool hasPrevPage,bool hasNextPage)
        {
            string seasonName=GGData.Seasons[season];
            hudSeason.text=seasonName.Substring(0,1)+seasonName.Substring(1).ToLowerInvariant();
            hudPage.text="Лента  "+(page+1)+" / "+Mathf.CeilToInt(delivered/(float)GGWorld.ItemsPerPage);
            hudFinish.gameObject.SetActive(canFinish);
            hudFinish.interactable=!busy;
            hudPrev.interactable=hasPrevPage&&!busy;
            hudNext.interactable=hasNextPage&&!busy;
        }
        private void BuildMenu()
        {
            menu=FullScreen("Главное меню");
            ImagePanel(menu.transform,"Сплошной фон",Vector2.zero,Vector2.one,
                Vector2.zero,Vector2.zero,new Color(.055f,.11f,.15f,1),false);
            var center=ImagePanel(menu.transform,"Меню",new Vector2(.27f,.18f),new Vector2(.73f,.82f),
                Vector2.zero,Vector2.zero,Color.clear,false);
            Label(center,"Годный год",new Vector2(.01f,.66f),new Vector2(.99f,.88f),74,white,TextAnchor.MiddleCenter,true);
            ImagePanel(center,"Линия под названием",new Vector2(.39f,.64f),new Vector2(.61f,.643f),
                Vector2.zero,Vector2.zero,new Color(.72f,.52f,.32f,1),false);
            MakeButton(center,"Начать",new Vector2(.31f,.415f),new Vector2(.69f,.505f),
                new Color(.83f,.62f,.40f,1),ink,29,()=>game.StartRun(),false);
            MakeButton(center,"Обучение",new Vector2(.31f,.29f),new Vector2(.69f,.38f),
                new Color(.15f,.23f,.26f,1),white,25,()=>ShowHelp(),false);
            MakeButton(center,"Выход",new Vector2(.31f,.17f),new Vector2(.69f,.26f),
                new Color(.10f,.17f,.20f,1),white,24,()=>game.QuitGame(),false);
        }
        private void BuildHelp()
        {
            instructions=FullScreen("Обучение");
            ImagePanel(instructions.transform,"Фон",Vector2.zero,Vector2.one,
                Vector2.zero,Vector2.zero,new Color(.055f,.11f,.15f,1),false);
            var pane=ImagePanel(instructions.transform,"Инструктаж",
                new Vector2(.16f,.12f),new Vector2(.84f,.86f),Vector2.zero,Vector2.zero,panel,true);
            Label(pane,"ИНСТРУКТАЖ",new Vector2(.06f,.79f),new Vector2(.94f,.90f),
                43,cream,TextAnchor.MiddleLeft,true);
            Label(pane,
                "Ваша цель — собирать хорошие годы.\n\n"+
                "• Выберите обстоятельство на конвейере и положите его в коробку нужного месяца.\n\n"+
                "• В каждой коробке должно быть по три обстоятельства.\n\n"+
                "• Учитывайте сезон и сочетание событий.\n\n"+
                "• Лишние обстоятельства можно оставить на ленте — важен выбор лучших сочетаний.",
                new Vector2(.07f,.24f),new Vector2(.93f,.75f),24,white,TextAnchor.UpperLeft);
            MakeButton(pane,"НАЗАД",new Vector2(.33f,.055f),new Vector2(.67f,.15f),
                new Color(.93f,.73f,.39f,1),ink,29,()=>ShowMenu());
        }
        private void BuildHud()
        {
            hud=FullScreen("Интерфейс смены");
            BuildWorldHitAreas();
            var seasonTag=ImagePanel(hud.transform,"Бирка сезона",new Vector2(.025f,.928f),new Vector2(.125f,.978f),
                Vector2.zero,Vector2.zero,new Color(.09f,.13f,.15f,.91f),false);
            hudSeason=Label(seasonTag,"Весна",new Vector2(.06f,.04f),new Vector2(.94f,.96f),22,white,TextAnchor.MiddleCenter);
            MakeButton(hud.transform,"Пауза",new Vector2(.909f,.928f),new Vector2(.976f,.978f),
                new Color(.11f,.17f,.20f,.96f),white,19,()=>game.TogglePause(),false);

            var mid=ImagePanel(hud.transform,"Управление лентой",new Vector2(.375f,.018f),new Vector2(.625f,.078f),
                Vector2.zero,Vector2.zero,new Color(.09f,.13f,.15f,.94f),false);
            hudPage=Label(mid,"Лента  1 / 3",new Vector2(.22f,.05f),new Vector2(.78f,.95f),23,white,TextAnchor.MiddleCenter);
            hudPrev=MakeButton(mid,"‹",new Vector2(.025f,.10f),new Vector2(.205f,.90f),new Color(.22f,.30f,.32f,1),white,31,()=>game.ChangePage(-1),false);
            hudNext=MakeButton(mid,"›",new Vector2(.795f,.10f),new Vector2(.975f,.90f),new Color(.22f,.30f,.32f,1),white,31,()=>game.ChangePage(1),false);
            hudFinish=MakeButton(hud.transform,"Отправить сезон",new Vector2(.775f,.018f),new Vector2(.976f,.078f),
                new Color(.83f,.62f,.40f,1),ink,24,()=>game.FinishSeason(),false);
        }
        private void BuildWorldHitAreas()
        {
            var layer=new GameObject("КЛИКАБЕЛЬНЫЕ ОБЛАСТИ НАД 3D-ПРЕДМЕТАМИ",typeof(RectTransform));
            layer.transform.SetParent(hud.transform,false);
            hitLayer=layer.GetComponent<RectTransform>();
            hitLayer.anchorMin=Vector2.zero;hitLayer.anchorMax=Vector2.one;
            hitLayer.offsetMin=Vector2.zero;hitLayer.offsetMax=Vector2.zero;
            for(int i=0;i<GGWorld.ItemsPerPage;i++)
            {
                int slot=i;
                itemHitAreas[i]=MakeHitButton(layer.transform,"Предмет "+i,()=>game.SelectItemFromHitArea(currentPage*GGWorld.ItemsPerPage+slot));
            }
            for(int i=0;i<3;i++)
            {
                int box=i;
                boxHitAreas[i]=MakeHitButton(layer.transform,"Коробка "+i,()=>game.PlaceSelectedFromHitArea(box));
            }
        }
        private int currentPage;
        private RectTransform MakeHitButton(Transform parent,string name,Action click)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));
            go.transform.SetParent(parent,false);
            var rt=go.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);
            rt.pivot=new Vector2(.5f,.5f);
            var image=go.GetComponent<Image>();image.color=new Color(1,1,1,0);image.raycastTarget=true;
            var button=go.GetComponent<Button>();button.targetGraphic=image;
            button.transition=Selectable.Transition.None;
            button.onClick.AddListener(()=>click());
            return rt;
        }
        private void SetHitRect(RectTransform rt,Camera cam,Vector3 middle,Vector3 halfBounds,float minW,float minH)
        {
            if(rt==null)return;
            var parentRect=hitLayer;
            float lowX=float.PositiveInfinity,lowY=float.PositiveInfinity;
            float highX=float.NegativeInfinity,highY=float.NegativeInfinity;
            for(int x=-1;x<=1;x+=2)
            for(int y=-1;y<=1;y+=2)
            for(int z=-1;z<=1;z+=2)
            {
                Vector3 pos=middle+Vector3.Scale(halfBounds,new Vector3(x,y,z));
                Vector3 screen=cam.WorldToScreenPoint(pos);
                if(screen.z<=0){rt.gameObject.SetActive(false);return;}
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect,new Vector2(screen.x,screen.y),null,out local);
                lowX=Mathf.Min(lowX,local.x);highX=Mathf.Max(highX,local.x);
                lowY=Mathf.Min(lowY,local.y);highY=Mathf.Max(highY,local.y);
            }
            rt.gameObject.SetActive(true);
            rt.anchoredPosition=new Vector2((lowX+highX)*.5f,(lowY+highY)*.5f);
            rt.sizeDelta=new Vector2(Mathf.Max(highX-lowX,minW),Mathf.Max(highY-lowY,minH));
        }
        public void SyncWorldHitAreas(GGWorld world,int page,int[] status,bool canClick)
        {
            if(hitLayer==null||world==null||world.sceneCamera==null)return;
            currentPage=page;
            for(int slot=0;slot<GGWorld.ItemsPerPage;slot++)
            {
                var rt=itemHitAreas[slot];
                int index=page*GGWorld.ItemsPerPage+slot;
                bool visible=canClick && status!=null && index<status.Length && status[index]==0
                    && world.items!=null && index<world.items.Length && world.items[index]!=null && world.items[index].gameObject.activeInHierarchy;
                if(!visible){rt.gameObject.SetActive(false);continue;}
                var item=world.items[index];
                SetHitRect(rt,world.sceneCamera,item.transform.position+new Vector3(0,.69f,0),new Vector3(1.10f,.89f,.69f),110,80);
            }
            for(int i=0;i<3;i++)
            {
                var rt=boxHitAreas[i];
                bool visible=canClick && world.boxes!=null && world.boxes[i]!=null;
                if(!visible){rt.gameObject.SetActive(false);continue;}
                var b=world.boxes[i];
                SetHitRect(rt,world.sceneCamera,b.transform.position+new Vector3(0,.55f,0),new Vector3(1.27f,.63f,1.05f),140,95);
            }
        }
        private void BuildDialogue()
        {
            dialogue=FullScreen("Диалог с начальником");
            ImagePanel(dialogue.transform,"Затемнение",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.03f,.07f,.10f,.45f),false);
            var d=ImagePanel(dialogue.transform,"Диалоговое окно",new Vector2(.11f,.055f),new Vector2(.89f,.265f),Vector2.zero,Vector2.zero,panel,false);
            ImagePanel(d,"Акцент",new Vector2(0,0),new Vector2(.008f,1),Vector2.zero,Vector2.zero,new Color(.92f,.70f,.37f,1),false);
            dialogueSpeaker=Label(d,"НАЧАЛЬНИК СМЕНЫ",new Vector2(.05f,.73f),new Vector2(.80f,.94f),25,cream,TextAnchor.MiddleLeft,true);
            dialogueText=Label(d,"Текст диалога",new Vector2(.05f,.25f),new Vector2(.94f,.73f),27,white,TextAnchor.MiddleLeft);
            var button=MakeButton(d,"ДАЛЕЕ  →",new Vector2(.77f,.055f),new Vector2(.96f,.25f),new Color(.83f,.62f,.40f,1),ink,20,()=>game.NextDialogue(),false);
            dialogueNextText=button.GetComponentInChildren<Text>();
            Label(dialogue.transform,"НАЖМИ ПРОБЕЛ ИЛИ КНОПКУ, ЧТОБЫ ПРОДОЛЖИТЬ",new Vector2(.24f,.012f),new Vector2(.76f,.054f),18,muted,TextAnchor.MiddleCenter);
        }
        private void BuildReport()
        {
            report=FullScreen("Отчёт");
            ImagePanel(report.transform,"Затемнение",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.02f,.05f,.07f,.89f),false);
            var paper=new Color(.93f,.89f,.80f,1);
            var print=new Color(.32f,.33f,.30f,1);
            var card=ImagePanel(report.transform,"Бланк отчёта",new Vector2(.24f,.17f),new Vector2(.76f,.83f),
                Vector2.zero,Vector2.zero,paper,false);
            Label(card,"Фабрика времени",new Vector2(.075f,.88f),new Vector2(.92f,.96f),19,print,TextAnchor.MiddleLeft,true);
            ImagePanel(card,"Линия бланка",new Vector2(.075f,.859f),new Vector2(.925f,.862f),Vector2.zero,Vector2.zero,print,false);
            reportHeading=Label(card,"Отчёт",new Vector2(.075f,.715f),new Vector2(.925f,.84f),43,ink,TextAnchor.MiddleLeft,true);
            reportBody=Label(card,"Текст отчёта",new Vector2(.075f,.32f),new Vector2(.925f,.68f),25,ink,TextAnchor.UpperLeft,false,1.16f);
            reportScore=Label(card,"",new Vector2(.61f,.50f),new Vector2(.925f,.685f),51,ink,TextAnchor.MiddleRight,true);
            reportVerdict=Label(card,"",new Vector2(.45f,.39f),new Vector2(.925f,.51f),22,new Color(.52f,.25f,.17f),TextAnchor.MiddleRight,true);
            ImagePanel(card,"Линия примечания",new Vector2(.075f,.305f),new Vector2(.925f,.307f),Vector2.zero,Vector2.zero,print,false);
            reportNote=Label(card,"Комментарий",new Vector2(.075f,.17f),new Vector2(.925f,.295f),22,print,TextAnchor.MiddleLeft);
            reportActionButton=MakeButton(card,"Дальше",new Vector2(.56f,.045f),new Vector2(.925f,.143f),ink,white,23,()=>onReportAction?.Invoke(),false);
            reportActionLabel=reportActionButton.GetComponentInChildren<Text>();
        }

        private GameObject FullScreen(string name)
        {
            var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(canvas.transform,false);
            var rt=g.GetComponent<RectTransform>();rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=Vector2.zero;rt.offsetMax=Vector2.zero;
            return g;
        }
        private RectTransform ImagePanel(Transform parent,string name,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax,Color tint,bool round)
        {
            var g=new GameObject(name,typeof(RectTransform),typeof(Image));g.transform.SetParent(parent,false);
            var rt=g.GetComponent<RectTransform>();rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=offsetMin;rt.offsetMax=offsetMax;
            var im=g.GetComponent<Image>();im.color=tint;im.raycastTarget=false;
            if(round){im.sprite=rounded;im.type=Image.Type.Sliced;}return rt;
        }
        private Text Label(Transform parent,string content,Vector2 min,Vector2 max,int fontSize,Color color,TextAnchor align,bool bold=false,float lineSpacing=1.06f)
        {
            var g=new GameObject("Текст — "+content.Substring(0,Mathf.Min(content.Length,17)),typeof(RectTransform),typeof(Text));g.transform.SetParent(parent,false);
            var rt=g.GetComponent<RectTransform>();rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=Vector2.zero;rt.offsetMax=Vector2.zero;
            var label=g.GetComponent<Text>();label.font=font;label.text=content;label.fontSize=fontSize;label.alignment=align;
            label.color=color;label.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;label.lineSpacing=lineSpacing;
            label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;
            label.raycastTarget=false;
            return label;
        }
        private Button MakeButton(Transform parent,string content,Vector2 min,Vector2 max,Color bg,Color text,int size,Action callback,bool round=true)
        {
            var rect=ImagePanel(parent,"Кнопка — "+content,min,max,Vector2.zero,Vector2.zero,bg,round);
            var image=rect.GetComponent<Image>();image.raycastTarget=true;
            var b=rect.gameObject.AddComponent<Button>();b.targetGraphic=image;
            var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.11f,1.11f,1.08f,1);
            colors.pressedColor=new Color(.82f,.82f,.82f,1);colors.disabledColor=new Color(.45f,.45f,.45f,.75f);b.colors=colors;
            Label(rect,content,new Vector2(.02f,.02f),new Vector2(.98f,.98f),size,text,TextAnchor.MiddleCenter,true);
            b.onClick.AddListener(()=>callback?.Invoke());return b;
        }
        private Sprite MakeRoundedSprite()
        {
            const int n=64;var tex=new Texture2D(n,n,TextureFormat.RGBA32,false);
            tex.wrapMode=TextureWrapMode.Clamp;
            var px=new Color[n*n];float r=14;
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float cx=Mathf.Clamp(x, r,n-r),cy=Mathf.Clamp(y,r,n-r);
                float d=Vector2.Distance(new Vector2(x,y),new Vector2(cx,cy));
                float alpha=Mathf.Clamp01(r-d+.5f);px[y*n+x]=new Color(1,1,1,alpha);
            }
            tex.SetPixels(px);tex.Apply();
            return Sprite.Create(tex,new Rect(0,0,n,n),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(16,16,16,16));
        }
    }
}
