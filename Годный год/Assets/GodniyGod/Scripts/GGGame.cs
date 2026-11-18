using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GodniyGod
{
    // Attach to one GameObject in Assets/GodniyGod/Scenes/Main.unity.
    // The factory, camera, UI, lighting and interaction objects are spawned automatically at runtime.
    public sealed class GGGame : MonoBehaviour
    {
        private enum GameMode { Menu, Help, Dialogue, Working, Report, Final }
        private GameMode mode=GameMode.Menu;
        private GGUI gui;
        private GGWorld world;
        private readonly List<GGYearRecord> history=new List<GGYearRecord>();
        private GGYearRecord currentYear;
        private List<GGEvent> delivered=new List<GGEvent>();
        private List<GGEvent>[] placedEvents;
        private int[] itemStatus;
        private int[] boxCounts;
        private int year, season, page, selected, placed, runSeed;
        private bool animating;
        private readonly List<DialogueLine> lines=new List<DialogueLine>();
        private int lineIndex;
        private Action afterDialogue;
        private AudioSource sounds;
        private AudioSource factoryAmbience;
        private AudioSource menuMusic;
        private AudioClip clickSound, stampSound;
        private bool isPaused;
        private bool factorySoundStarted;
        private bool menuMusicStarted;
        private bool endingDialogue;

        private struct DialogueLine
        {
            public readonly string Who, Text;
            public DialogueLine(string speaker,string message){Who=speaker;Text=message;}
        }
        private void Start()
        {
            Time.timeScale=1f;
            Application.targetFrameRate=60;
            if(EventSystem.current==null)
                new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
            
            gui=new GGUI(this);gui.Build();
            world=new GGWorld(gui.font);
            if(!world.BindSavedFactory()) world.Build();
            world.BuildSeason(0,0,GGData.GetDelivery(0,0));
            sounds=gameObject.AddComponent<AudioSource>();sounds.playOnAwake=false;sounds.volume=.26f;
            clickSound=GenerateSound(460,.08f);stampSound=GenerateSound(190,.22f);
            StartFactoryAmbience();
            CreateMenuMusic();
            mode=GameMode.Menu;gui.ShowMenu();
            PlayMenuMusic();
        }
        /// <summary>Quiet 2D factory ambience only while the production is running.</summary>
        private void StartFactoryAmbience()
        {
            AudioClip recording=Resources.Load<AudioClip>("Audio/factory_conveyor");
            if(recording==null)
            {
                Debug.LogWarning("Годный год: не удалось загрузить Audio/factory_conveyor.mp3 из Resources.");
                return;
            }
            var ambienceObject=new GameObject("Фоновый звук фабрики (цикл)");
            ambienceObject.transform.SetParent(transform,false);
            factoryAmbience=ambienceObject.AddComponent<AudioSource>();
            factoryAmbience.playOnAwake=false;
            factoryAmbience.spatialBlend=0f;
            factoryAmbience.loop=true;
            factoryAmbience.volume=0.12f;
            factoryAmbience.clip=recording;
        }

        private void CreateMenuMusic()
        {
            AudioClip clip=Resources.Load<AudioClip>("Audio/menu_theme");
            if(clip==null)
            {
                Debug.LogWarning("Годный год: не найдена музыка Audio/menu_theme в Resources.");
                return;
            }
            var player=new GameObject("Музыка главного меню и паузы");
            player.transform.SetParent(transform,false);
            menuMusic=player.AddComponent<AudioSource>();
            menuMusic.playOnAwake=false;
            menuMusic.spatialBlend=0f;
            menuMusic.loop=true;
            menuMusic.volume=.22f;
            menuMusic.clip=clip;
            menuMusic.ignoreListenerPause=true;
        }

        private void PlayMenuMusic()
        {
            if(factoryAmbience!=null && factoryAmbience.isPlaying)factoryAmbience.Pause();
            if(menuMusic==null)return;
            if(!menuMusicStarted){menuMusic.Play();menuMusicStarted=true;}
            else menuMusic.UnPause();
        }

        private void PlayFactorySound()
        {
            if(menuMusic!=null && menuMusic.isPlaying)menuMusic.Pause();
            if(factoryAmbience==null)return;
            if(!factorySoundStarted){factoryAmbience.Play();factorySoundStarted=true;}
            else factoryAmbience.UnPause();
        }

        public void TogglePause()
        {
            if(isPaused){ResumeGame();return;}
            if(mode==GameMode.Menu || mode==GameMode.Help || mode==GameMode.Final)return;
            isPaused=true;
            Time.timeScale=0f;
            if(gui!=null)gui.ShowPause();
            PlayMenuMusic();
        }

        public void ResumeGame()
        {
            if(!isPaused)return;
            isPaused=false;
            Time.timeScale=1f;
            if(gui!=null)gui.ClosePause();
            PlayFactorySound();
        }

        public void ExitToMainMenu()
        {
            world?.ResetFactoryWear();
            // Don't let unfinished item tweens complete in a new run.
            DG.Tweening.DOTween.KillAll(false);
            Time.timeScale=1f;
            isPaused=false;
            animating=false;
            selected=-1;
            mode=GameMode.Menu;
            if(gui!=null){gui.ClosePause();gui.ShowMenu();}
            PlayMenuMusic();
        }

        private void OnDestroy()
        {
            // Time.timeScale is global; never leave Unity slowed after stopping Play Mode.
            Time.timeScale=1f;
            world?.DisposeFactoryWear();
        }

        private void Update()
        {
            bool escapePressed=false;
#if ENABLE_LEGACY_INPUT_MANAGER
            escapePressed=Input.GetKeyDown(KeyCode.Escape);
#endif
#if ENABLE_INPUT_SYSTEM
            escapePressed=escapePressed || (UnityEngine.InputSystem.Keyboard.current!=null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame);
#endif
            if(escapePressed)
            {
                TogglePause();
                return;
            }
            if(isPaused)return;
#if ENABLE_LEGACY_INPUT_MANAGER
            if(mode==GameMode.Dialogue && (Input.GetKeyDown(KeyCode.Space)||Input.GetKeyDown(KeyCode.Return)))
            {NextDialogue();return;}
#endif
            if(mode==GameMode.Working && gui!=null && world!=null)
                gui.SyncWorldHitAreas(world,page,itemStatus,mode==GameMode.Working&&!animating);
            if(mode!=GameMode.Working||animating)return;
#if ENABLE_LEGACY_INPUT_MANAGER
            if(Input.GetKeyDown(KeyCode.LeftArrow)||Input.GetKeyDown(KeyCode.A))ChangePage(-1);
            if(Input.GetKeyDown(KeyCode.RightArrow)||Input.GetKeyDown(KeyCode.D))ChangePage(1);
            if(!Input.GetMouseButtonDown(0))return;
            if(EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject())return;
            Ray ray=world.sceneCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if(!Physics.Raycast(ray,out hit,100f))return;
            GGWorldItem item=hit.collider.GetComponentInParent<GGWorldItem>();
            if(item!=null){SelectItem(item.index);return;}
            GGWorldBox box=hit.collider.GetComponentInParent<GGWorldBox>();
            if(box!=null)PlaceSelected(box.index);
#endif
        }
        public void StartRun()
        {
            isPaused=false;
            Time.timeScale=1f;
            if(gui!=null)gui.ClosePause();
            PlayFactorySound();
            history.Clear();year=0;season=0;selected=-1;page=0;
            runSeed=Guid.NewGuid().GetHashCode();
            BeginYear();
        }
        private void BeginYear()
        {
            currentYear=new GGYearRecord{yearIndex=year};season=0;
            world.SetFactoryYear(year);
            if(year==0)
            {
                Dialogue(()=>BeginSeason(),
                    new DialogueLine("НАЧАЛЬНИК ЦЕХА", "Добро пожаловать на фабрику времени. Здесь мы собираем годы для всего человечества. Не волнуйтесь, обучение не предусмотрено."),
                    new DialogueLine("ВЫ", "А я точно должен решать, что произойдёт с человечеством?"),
                    new DialogueLine("НАЧАЛЬНИК ЦЕХА", "Разумеется. У нас нет бюджета на квалифицированных сотрудников. В каждом месяце должно оказаться по три обстоятельства."),
                    new DialogueLine("НАЧАЛЬНИК ЦЕХА", "Начнём с 2027-го. Сделайте год хоть немного годным. И не перепутайте месяцы. Хотя коробки выдают не мы."));
            }
            else if(year==1)
            {
                Dialogue(()=>BeginSeason(),
                    new DialogueLine("НАЧАЛЬНИК ЦЕХА", "2027-й прошёл. Некоторые жаловались, но в целом человечество ещё функционирует."),
                    new DialogueLine("НАЧАЛЬНИК ЦЕХА", "В 2028-м на сезон привозят по одиннадцать обстоятельств. Поставщика сменили."),
                    new DialogueLine("ВЫ", "А новые поставки хоть кто-нибудь проверял? И почему из ящика дым идёт?"),
                    new DialogueLine("НАЧАЛЬНИК ЦЕХА", "Нет. Просто оформите заявку."));
            }
            else
            {
                Dialogue(()=>BeginSeason(),
                    new DialogueLine("ВЫ", "У ящика крышка отваливается. Ремонтники так и не пришли?"),
                    new DialogueLine("НАЧАЛЬНИК ЦЕХА", "В системе заявка закрыта. На сезон теперь привозят по десять обстоятельств."),
                    new DialogueLine("ВЫ", "В прошлом году я мог оставить на ленте две ситуации. Теперь всего одну?"),
                    new DialogueLine("НАЧАЛЬНИК ЦЕХА", "Одну. Начинайте 2029-й."));
            }
        }
        private void BeginSeason()
        {
            delivered=GGData.GetDelivery(year,season,runSeed);
            world.BuildSeason(year,season,delivered);
            itemStatus=new int[delivered.Count];
            boxCounts=new int[3];placedEvents=new List<GGEvent>[3];
            for(int i=0;i<3;i++)placedEvents[i]=new List<GGEvent>();
            placed=0;page=0;selected=-1;animating=false;
            if(season==0 && year==1)
                Dialogue(EnterWork,new DialogueLine("ВЫ", "Наводнение? Если оставлю его на ленте, почти всё остальное придётся взять."));
            else if(season==0 && year==2)
                Dialogue(EnterWork,new DialogueLine("ВЫ", "Вулкан и авария на подстанции в одной поставке. И хотя бы одно из этого придётся положить в год."));
            else EnterWork();
        }
        private void EnterWork()
        {
            mode=GameMode.Working;gui.ShowHud();UpdateHud();
        }
        // UI button hotspots over the 3D items make the game clickable even if
        // the imported model's mesh collider / camera raycast is unreliable.
        public void SelectItemFromHitArea(int index)
        {
            if(mode==GameMode.Working)SelectItem(index);
        }
        public void PlaceSelectedFromHitArea(int index)
        {
            if(mode==GameMode.Working)PlaceSelected(index);
        }
        private void SelectItem(int index)
        {
            if(index<0||index>=itemStatus.Length||itemStatus[index]!=0||animating)return;
            if(selected==index){world.SelectItem(world.items[index],false);selected=-1;}
            else
            {
                if(selected>=0&&selected<world.items.Length)world.SelectItem(world.items[selected],false);
                selected=index;world.SelectItem(world.items[selected],true);
            }
            Click();UpdateHud();
        }
        private void PlaceSelected(int box)
        {
            if(selected<0||box<0||box>=3||animating)return;
            if(boxCounts[box]>=3){world.AnimateCompletion();Click();return;}
            int index=selected;selected=-1;
            var token=world.items[index];
            if(token==null||itemStatus[index]!=0)return;
            animating=true;itemStatus[index]=1;boxCounts[box]++;placed++;
            placedEvents[box].Add(delivered[index]);world.RefreshBox(box,boxCounts[box]);
            world.MarkItemRemoved(token);
            Vector3 target=world.GetBoxDrop(box);
            world.SelectItem(token,false);
            GGAnimation.PutInBox(token.transform,target,() =>
            {
                if(token!=null)token.gameObject.SetActive(false);
                animating=false;
                if(placed<9)AutoAdvancePageIfNeeded();
                UpdateHud();
            });
            Click();UpdateHud();
        }
        private bool PageHasAvailableItems(int p)
        {
            if(itemStatus==null || p<0)return false;
            int start=p*GGWorld.ItemsPerPage;
            if(start>=itemStatus.Length)return false;
            int end=Mathf.Min(itemStatus.Length,start+GGWorld.ItemsPerPage);
            for(int i=start;i<end;i++)if(itemStatus[i]==0)return true;
            return false;
        }
        // Manual arrows skip already empty pages. A missing neighbour disables its arrow.
        private int FindAvailablePage(int direction)
        {
            int max=Mathf.CeilToInt(delivered.Count/(float)GGWorld.ItemsPerPage);
            for(int p=page+direction;p>=0 && p<max;p+=direction)
                if(PageHasAvailableItems(p))return p;
            return -1;
        }
        private void AutoAdvancePageIfNeeded()
        {
            if(PageHasAvailableItems(page))return;
            int next=FindAvailablePage(1);
            if(next<0)
            {
                // If the player went backwards, still visit earlier nonempty pages.
                int max=Mathf.CeilToInt(delivered.Count/(float)GGWorld.ItemsPerPage);
                for(int p=0;p<max;p++)
                    if(PageHasAvailableItems(p)){next=p;break;}
            }
            if(next>=0 && next!=page)StartPageTransition(next);
        }
        private void StartPageTransition(int next)
        {
            if(next<0||next==page||!PageHasAvailableItems(next))return;
            if(selected>=0&&selected<world.items.Length)
                world.SelectItem(world.items[selected],false);
            selected=-1;
            int before=page;
            page=next;
            animating=true;
            UpdateHud();
            world.SlideToPage(before,next,() =>
            {
                animating=false;
                UpdateHud();
            });
        }
        public void ChangePage(int direction)
        {
            if(mode!=GameMode.Working||animating||placed>=9)return;
            int next=FindAvailablePage(direction);
            if(next<0)return;
            StartPageTransition(next);
            Click();
        }
        private void UpdateHud()
        {
            if(gui==null||mode!=GameMode.Working)return;
            gui.RefreshHud(season,delivered.Count,page,placed==9,animating,
                placed<9 && FindAvailablePage(-1)>=0,placed<9 && FindAvailablePage(1)>=0);
        }
        public void FinishSeason()
        {
            if(mode!=GameMode.Working||animating||placed!=9)return;
            if(selected>=0){world.SelectItem(world.items[selected],false);selected=-1;}
            int seasonalScore=0;
            var monthRecords=new List<GGMonthRecord>();
            for(int i=0;i<3;i++)
            {
                var record=new GGMonthRecord{name=world.boxes[i].month,season=season,slot=i};
                record.events.AddRange(placedEvents[i]);record.score=GGData.ScoreMonth(record);
                seasonalScore+=record.score;monthRecords.Add(record);currentYear.months.Add(record);
            }
            mode=GameMode.Report;
            world.AnimateCompletion();Stamp();
            ShowSeasonReport(monthRecords,seasonalScore);
        }
        private void ShowSeasonReport(List<GGMonthRecord> records,int score)
        {
            // Scores and combinations are saved for the YEAR report only.
            // A packed season is a one-line transition, not another long form.
            gui.ShowSeasonPacked(season,AfterSeasonReport);
        }
        private void AfterSeasonReport()
        {
            if(season<3){season++;BeginSeason();}
            else EndYear();
        }
        private void EndYear()
        {
            GGData.EvaluateYear(currentYear);history.Add(currentYear);
            StringBuilder body=new StringBuilder();
            body.Append("По сезонам\n");
            string[] seasonNames={"Весна","Лето","Осень","Зима"};
            for(int i=0;i<4;i++)
            {
                int points=0, months=0;
                foreach(var month in currentYear.months)
                    if(month.season==i){points+=month.score;months++;}
                body.Append(seasonNames[i]).Append("    ").Append(GGData.QualityPercent(points,months)).Append(" / 100\n");
            }
            string comment;
            if(year==0) comment=currentYear.happiness>=78
                ?"Начальник: «Год получился хорошим. Значит, в следующем можно немного сэкономить»."
                :"Начальник: «Есть жалобы, но в целом год можно выпускать».";
            else if(year==1) comment=currentYear.happiness<history[0].happiness
                ?"Начальник: «Жители говорят, что этот год вышел хуже прошлого. Жалобы уже у нас»."
                :"Начальник: «Жители жалуются на перебои. Даже если общий балл не упал, проблемы они почувствовали».";
            else comment="Начальник цеха просит вас остаться после смены.";
            gui.ShowReport("Отчёт за "+GGData.YearNames[year]+" год",body.ToString(),comment,
                year==2?"К начальнику":"Следующий год",AfterYearReport,currentYear.happiness+" / 100",currentYear.grade);
        }
        private void AfterYearReport()
        {
            if(year<2){year++;BeginYear();}
            else
            {
                Dialogue(ShowFinal,
                    new DialogueLine("НАЧАЛЬНИК ЦЕХА", "Я посмотрел отчёт. Что вы положили в последний год? Жалобы до сих пор идут."),
                    new DialogueLine("ВЫ", "То, что нам привезли. Осенью, например, в партии была прорванная дамба. Остальные варианты были не сильно лучше."),
                    new DialogueLine("НАЧАЛЬНИК ЦЕХА", "Под отчётом стоит ваша подпись. Отдел закупок туда вообще не вписывают."),
                    new DialogueLine("ВЫ", "Я ещё во втором году просил проверить поставки. А вы сами материалы видели?"),
                    new DialogueLine("НАЧАЛЬНИК ЦЕХА", "Нет. Вы уволены."));
            }
        }
        private void ShowFinal()
        {
            mode=GameMode.Final;
            StringBuilder b=new StringBuilder();
            b.Append("Качество по годам\n");
            foreach(var record in history)
                b.Append(GGData.YearNames[record.yearIndex]).Append("    ")
                    .Append(record.happiness).Append(" / 100\n");
            gui.ShowReport("Итоги работы",b.ToString(),
                "Наутро за тот же конвейер встал новый сотрудник. Ему тоже сказали: «Сделайте год хоть немного годным».",
                "В главное меню",ReturnMenu,"","Уволен");
            Stamp();
        }
        private void ReturnMenu(){ExitToMainMenu();}
        private void Dialogue(Action done,params DialogueLine[] conversation)
        {
            lines.Clear();lines.AddRange(conversation);lineIndex=0;afterDialogue=done;
            endingDialogue=done==((Action)ShowFinal);
            mode=GameMode.Dialogue;DisplayDialogue();
        }
        private void DisplayDialogue()
        {
            if(lineIndex<0||lineIndex>=lines.Count)return;
            var line=lines[lineIndex];gui.ShowDialogue(line.Who,line.Text,lineIndex>=lines.Count-1,endingDialogue);
        }
        public void NextDialogue()
        {
            if(mode!=GameMode.Dialogue)return;
            Click();lineIndex++;
            if(lineIndex<lines.Count){DisplayDialogue();return;}
            var action=afterDialogue;afterDialogue=null;lines.Clear();action?.Invoke();
        }
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
        private void Click(){if(sounds!=null&&clickSound!=null)sounds.PlayOneShot(clickSound,.55f);}
        private void Stamp(){if(sounds!=null&&stampSound!=null)sounds.PlayOneShot(stampSound,.72f);}
        private AudioClip GenerateSound(float frequency,float length)
        {
            int hz=22050;int total=Mathf.CeilToInt(hz*length);
            var clip=AudioClip.Create("Фабричный сигнал",total,1,hz,false);
            float[] samples=new float[total];
            for(int i=0;i<total;i++)
            {
                float x=i/(float)hz,fade=Mathf.Sin(Mathf.PI*i/total);
                samples[i]=Mathf.Sin(x*frequency*2*Mathf.PI)*fade*.2f;
            }
            clip.SetData(samples,0);return clip;
        }
    }
}
