using System;
using UnityEngine;

namespace GodniyGod
{
    // Small physical dioramas on the conveyor. All geometry is actual 3D primitives;
    // the separate caption names the circumstance without encoding its score.
    public static class GGEventVisuals
    {
        private static Transform root;
        private static Func<Color,Material> makeMat;
        private static Material white, stone, yellow, blue, red, green, bark, steel, amber, dark;
        private static readonly Color cWhite=new Color(.96f,.94f,.87f);
        private static readonly Color cBlue=new Color(.30f,.65f,.88f);
        private static readonly Color cGreen=new Color(.33f,.73f,.44f);
        private static readonly Color cDark=new Color(.12f,.22f,.36f);
        private static readonly Color cRed=new Color(.92f,.43f,.35f);
        private static readonly Color cYellow=new Color(.98f,.78f,.29f);

        public static void Build(GGEvent ev,Transform parent,Func<Color,Material> mat)
        {
            root=new GameObject("3D миниатюра · "+ev.title).transform;
            root.SetParent(parent,false);
            root.localPosition=new Vector3(0,.13f,.11f);
            makeMat=mat;
            white=M(cWhite);blue=M(cBlue);red=M(cRed);green=M(cGreen);
            yellow=M(cYellow);dark=M(cDark);stone=M(new Color(.58f,.69f,.78f));
            bark=M(new Color(.67f,.36f,.16f));steel=M(new Color(.45f,.63f,.78f));amber=M(new Color(1.00f,.47f,.13f));

            if(ev.isWeather) {Weather(ev.id);return;}
            switch(ev.id)
            {
                case "earthquake":Earthquake();break;
                case "volcano":Volcano();break;
                case "flood":case "damBreach":Flood(ev.id=="damBreach");break;
                case "landslide":case "rockfall":Rockslide(ev.id=="landslide");break;
                case "bridgeClosure":ClosedBridge();break;
                case "blackout":Power();break;
                case "bakery":Bakery();break;
                case "bookSwap":case "libraryHours":case "library":Books();break;
                case "streetMusic":Music();break;
                case "recycling":Recycling();break;
                case "communityGarden":Garden();break;
                case "clinicHours":Hospital("newHospital");break;
                case "courierDelay":Mail();break;
                case "pedestrianStreet":Walkway();break;
                case "roadWorks":case "longRepairs":Roadworks();break;
                case "traffic":case "transport":case "betterTrains":case "freeTransport":case "fuel":case "trafficFine":case "busSchedule":case "schedule":case "newCrosswalk":Bus();break;
                case "bonus":case "raise":case "pricesDown":case "priceRise":case "salaryDelay":case "utility":case "tax":case "rent":case "housing": Money(ev.id);break;
                case "park":case "cityCleanup":case "harvest":case "fires":case "springCleanup":case "sportsGround":Tree(ev.id);break;
                case "newHospital":case "closedClinic":case "flu":case "healthCheck":Hospital(ev.id);break;
                case "noHotWater":case "water":case "waterLimits":case "heating":Pipes();break;
                case "power":case "phone":case "streetLight":Power();break;
                case "vacation":case "holidays":case "schoolHoliday":case "shortWeek":case "workSat":case "newYearHoliday":case "festival":case "museums":case "festivalPrep":case "exams":Calendar(ev.id);break;
                case "layoffs":case "closures":case "remote":case "weekendMarket":Office(ev.id);break;
                default: Package();break;
            }
        }
        private static Material M(Color c){return makeMat(c);}
        private static GameObject O(string name,PrimitiveType type,Vector3 at,Vector3 sz,Material mat,Vector3? rot=null)
        {
            var g=GameObject.CreatePrimitive(type);g.name=name;
            g.transform.SetParent(root,false);
            g.transform.localPosition=at;g.transform.localScale=sz;
            if(rot.HasValue)g.transform.localRotation=Quaternion.Euler(rot.Value);
            g.GetComponent<Renderer>().sharedMaterial=mat;
            var col=g.GetComponent<Collider>();
#if UNITY_EDITOR
            if(!Application.isPlaying)UnityEngine.Object.DestroyImmediate(col);
            else
#endif
                UnityEngine.Object.Destroy(col);
            return g;
        }
        private static void Box(string name,float x,float y,float z,float sx,float sy,float sz,Material m)
        { O(name,PrimitiveType.Cube,new Vector3(x,y,z),new Vector3(sx,sy,sz),m); }
        private static void Ball(string name,float x,float y,float z,float sx,float sy,float sz,Material m)
        { O(name,PrimitiveType.Sphere,new Vector3(x,y,z),new Vector3(sx,sy,sz),m); }
        private static void Rod(string name,float x,float y,float z,float radius,float length,Material m,Vector3 rot)
        { O(name,PrimitiveType.Cylinder,new Vector3(x,y,z),new Vector3(radius,length*.5f,radius),m,rot); }

        private static void Cloud(Material shade)
        {
            Ball("Облако 1",-.33f,.83f,.12f,.60f,.34f,.40f,shade);
            Ball("Облако 2",.06f,.98f,.10f,.75f,.50f,.48f,shade);
            Ball("Облако 3",.40f,.80f,.12f,.52f,.34f,.43f,shade);
        }
        private static void Sun()
        {
            Ball("Солнце",0,.87f,.12f,.55f,.55f,.23f,yellow);
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI*.25f;
                var r=O("Солнечный луч",PrimitiveType.Cube,
                    new Vector3(Mathf.Sin(a)*.46f,.87f+Mathf.Cos(a)*.46f,.10f),
                    new Vector3(.085f,.25f,.07f),yellow);
                r.transform.localRotation=Quaternion.Euler(0,0,-i*45f);
            }
        }
        private static void Raindrops(Material c,int n)
        {
            // Keep the weather dioramas readable: three drops for drizzle, five for heavy rain.
            // Fixed spacing avoids the old dense 'curtain' and keeps the drops below the cloud.
            for(int i=0;i<n;i++)
            {
                float x=(i-(n-1)*.5f)*.27f;
                float y=.43f-(i%2)*.10f;
                Rod("Капля дождя",x,y,-.24f,.060f,.28f,c,new Vector3(0,0,25));
            }
        }
        private static void Snow()
        {
            Cloud(stone);
            for(int i=0;i<5;i++)
            {
                float x=(i-2)*.30f;
                float y=.34f+(i%2)*.11f;
                Rod("Луч снежинки 1",x,y,-.09f,.033f,.19f,white,new Vector3(0,0,0));
                Rod("Луч снежинки 2",x,y,-.09f,.033f,.19f,white,new Vector3(0,0,60));
                Rod("Луч снежинки 3",x,y,-.09f,.033f,.19f,white,new Vector3(0,0,-60));
            }
        }
        private static void Weather(string id)
        {
            switch(id)
            {
                case "sunny":case "warm":case "heat":case "dry":case "drought":case "comfortable":case "winterMild":Sun();break;
                case "snow":case "lightSnow":case "blizzard":case "ice":case "frost":case "coldSnap":case "steadySnow":Snow();break;
                case "lightRain":
                    Cloud(white);
                    Raindrops(blue,3);
                    break;
                case "rain":case "thunder":case "storm":case "hail":case "hurricane":case "quickShowers":
                    Cloud(stone);
                    Raindrops(blue,5);
                    break;
                case "fog":Cloud(white);Ball("Туман",0,.43f,.04f,1.5f,.30f,.5f,stone);break;
                case "mild":case "variableClouds":case "seasonalWeather":case "breeze":case "coolEvenings":Cloud(white);Sun();break;
                case "thaw":case "noSnow":Cloud(white);Ball("Проталина",0,.16f,0,1.35f,.14f,.70f,blue);break;
                default:Cloud(white);break;
            }
        }
        private static void House(float x,float z)
        {
            Box("Дом",x,.50f,z,.58f,.54f,.52f,white);
            O("Крыша",PrimitiveType.Cube,new Vector3(x,.83f,z),new Vector3(.72f,.15f,.63f),red);
            Box("Окно",x,.56f,z-.28f,.18f,.15f,.03f,blue);
        }
        private static void Earthquake()
        {
            Box("Участок земли",0,.12f,.1f,1.76f,.17f,.72f,bark);
            House(-.44f,.16f);
            O("Накренившийся дом",PrimitiveType.Cube,new Vector3(.47f,.48f,.12f),
                new Vector3(.53f,.58f,.46f),white,new Vector3(0,0,-18));
            O("Съехавшая крыша",PrimitiveType.Cube,new Vector3(.58f,.76f,.12f),
                new Vector3(.63f,.13f,.52f),red,new Vector3(0,0,-18));
            for(int i=0;i<4;i++)
                O("Трещина в земле",PrimitiveType.Cube,new Vector3(-.56f+i*.34f,.214f,-.19f),
                    new Vector3(.40f,.035f,.07f),dark,new Vector3(0,i%2==0?-18:18,0));
        }

        private static void Volcano()
        {
            for(int i=0;i<4;i++)
                O("Склон вулкана",PrimitiveType.Cube,new Vector3(0,.17f+i*.144f,.12f),
                    new Vector3(1.66f-i*.30f,.16f,1.02f-i*.15f),i==3?dark:bark,
                    new Vector3(0,15+i*10,0));
            Ball("Жерло",0,.668f,.12f,.49f,.08f,.34f,amber);
            Ball("Пепел",.08f,.82f,.13f,.44f,.31f,.36f,stone);
            Ball("Облако пепла",.33f,1.03f,.12f,.63f,.33f,.40f,stone);
            O("Лавовый поток",PrimitiveType.Cube,new Vector3(.32f,.37f,-.32f),
                new Vector3(.16f,.56f,.14f),red,new Vector3(33,0,36));
        }

        private static void Flood(bool dam)
        {
            Box("Уровень воды",0,.23f,.08f,1.76f,.20f,.91f,blue);
            if(dam)
            {
                Box("Левая стенка дамбы",-.54f,.51f,.18f,.52f,.58f,.25f,stone);
                O("Обломок дамбы",PrimitiveType.Cube,new Vector3(.53f,.46f,.18f),
                    new Vector3(.45f,.52f,.25f),stone,new Vector3(0,0,-24));
                Box("Поток в проломе",0,.38f,-.07f,.49f,.29f,.72f,blue);
            }
            else House(.02f,.17f);
            Box("Вода перед домом",0,.37f,-.25f,1.70f,.14f,.37f,blue);
            for(int i=0;i<3;i++) Box("Гребень волны",-.52f+i*.53f,.45f,-.38f,.31f,.04f,.06f,white);
        }

        private static void Rockslide(bool earth)
        {
            Box("Дорога",0,.11f,.04f,1.78f,.15f,.74f,dark);
            for(int i=0;i<3;i++)Box("Разметка",-.62f+i*.62f,.19f,-.08f,.22f,.018f,.055f,white);
            for(int i=0;i<5;i++)
                O("Обломок скалы",PrimitiveType.Cube,
                    new Vector3(-.57f+i*.28f,.23f+(i%3)*.14f,.20f+(i%2)*.08f),
                    new Vector3(.44f,.40f,.38f),earth?bark:stone,new Vector3(15+i*8,20+i*16,35-i*11));
        }

        private static void ClosedBridge()
        {
            Box("Река",0,.13f,.07f,1.77f,.15f,.86f,blue);
            Box("Настил",0,.36f,.1f,1.70f,.13f,.48f,steel);
            for(int i=-1;i<=1;i+=2)Box("Опора",i*.57f,.25f,.1f,.14f,.25f,.35f,stone);
            Box("Шлагбаум",0,.65f,-.16f,1.02f,.14f,.12f,red);
            for(int i=-1;i<=1;i++)Box("Полоса",i*.32f,.65f,-.23f,.14f,.13f,.025f,white);
        }

        private static void Bakery()
        {
            Box("Прилавок",0,.30f,.17f,1.30f,.39f,.62f,bark);
            for(int i=-1;i<=1;i++)Ball("Хлеб",i*.40f,.60f,.08f,.34f,.30f,.43f,amber);
            for(int i=-1;i<=1;i++)Box("Надрез на хлебе",i*.40f,.74f,.08f,.17f,.03f,.045f,white);
        }

        private static void Books()
        {
            Box("Книжная полка",0,.19f,.12f,1.52f,.15f,.58f,bark);
            Material[] spines={red,blue,green,yellow,steel};
            for(int i=0;i<5;i++)
                O("Книга",PrimitiveType.Cube,new Vector3(-.55f+i*.24f,.57f,.13f),
                    new Vector3(.19f,.62f-(i%2)*.14f,.40f),spines[i],new Vector3(0,0,i==4?-15:0));
        }

        private static void Music()
        {
            Box("Колонка",-.49f,.52f,.13f,.47f,.69f,.44f,dark);
            Ball("Динамик",-.49f,.51f,-.10f,.32f,.32f,.09f,stone);
            Rod("Микрофонная стойка",.37f,.51f,.05f,.045f,.72f,steel,Vector3.zero);
            Ball("Микрофон",.31f,.89f,.03f,.24f,.17f,.15f,amber);
        }

        private static void Recycling()
        {
            for(int i=-1;i<=1;i++)
            {
                Box("Контейнер",i*.46f,.47f,.1f,.38f,.59f,.47f,i==0?green:blue);
                Box("Крышка",i*.46f,.79f,.1f,.43f,.10f,.50f,steel);
                Box("Маркировка",i*.46f,.52f,-.147f,.16f,.15f,.025f,white);
            }
        }

        private static void Garden()
        {
            Box("Грядка",0,.22f,.16f,1.56f,.30f,.66f,bark);
            for(int i=-2;i<=2;i++)
            {
                Ball("Листья",i*.25f,.50f,.08f,.30f,.33f,.35f,green);
                Ball("Овощ",i*.25f,.38f,-.09f,.14f,.16f,.15f,amber);
            }
        }

        private static void Mail()
        {
            Box("Почтовый ящик",0,.52f,.13f,1.16f,.65f,.53f,blue);
            Box("Почтовая щель",0,.66f,-.15f,.72f,.09f,.035f,dark);
            Box("Письмо",.2f,.32f,-.18f,.40f,.25f,.04f,white);
        }

        private static void Walkway()
        {
            Box("Брусчатка",0,.14f,.07f,1.73f,.20f,.85f,stone);
            for(int i=-1;i<=1;i+=2)
                Rod("Столбик",i*.58f,.49f,-.17f,.10f,.56f,steel,Vector3.zero);
            Box("Скамейка",0,.48f,.16f,.68f,.14f,.31f,bark);
            for(int i=-1;i<=1;i+=2)Box("Ножка скамьи",i*.25f,.33f,.16f,.10f,.28f,.24f,dark);
        }

        private static void Roadworks()
        {
            Box("Дорога",0,.12f,.10f,1.62f,.10f,.75f,dark);
            for(int i=0;i<3;i++)Box("Разметка",-.52f+i*.5f,.18f,.08f,.24f,.012f,.055f,yellow);
            for(int i=0;i<2;i++)
            {
                float x=-.40f+i*.80f;
                O("Дорожный конус",PrimitiveType.Cylinder,new Vector3(x,.46f,-.07f),
                    new Vector3(.17f,.29f,.17f),amber);
                Box("Полоса конуса",x,.46f,-.23f,.26f,.07f,.02f,white);
            }
            Box("Ограждение",0,.81f,.20f,1.12f,.13f,.1f,red);
            Box("Белая полоса",0,.81f,.14f,.30f,.13f,.02f,white);
        }
        private static void Bus()
        {
            Box("Автобус",0,.50f,.05f,1.35f,.60f,.52f,yellow);
            Box("Лобовое окно",-.48f,.64f,-.225f,.35f,.23f,.035f,blue);
            for(int i=0;i<3;i++)Box("Окно",-.08f+i*.33f,.64f,-.225f,.24f,.23f,.036f,blue);
            for(int i=0;i<2;i++)
                Rod("Колесо",-.44f+i*.89f,.20f,-.21f,.19f,.13f,dark,new Vector3(90,0,0));
        }
        private static void Money(string id)
        {
            var coins=(id=="bonus"||id=="raise"||id=="pricesDown")?yellow:amber;
            for(int j=0;j<3;j++)for(int i=0;i<j+2;i++)
                O("Монета",PrimitiveType.Cylinder,new Vector3(-.49f+j*.45f,.21f+i*.11f,.11f),
                    new Vector3(.25f,.07f,.25f),coins);
            Box("Ценник",.52f,.80f,-.01f,.43f,.52f,.13f,white);
            Box("Штрих ценника",.52f,.90f,-.10f,.28f,.065f,.01f,dark);
        }
        private static void Tree(string id)
        {
            if(id=="harvest")
            {
                for(int i=0;i<3;i++)Ball("Яблоко",-.45f+i*.44f,.31f,.03f,.31f,.32f,.3f,i==1?red:green);
                Box("Лоток",0,.14f,.05f,1.48f,.09f,.6f,bark);
                return;
            }
            for(int i=0;i<2;i++)
            {
                float x=-.42f+i*.82f;
                Box("Ствол дерева",x,.46f,.10f,.16f,.65f,.15f,bark);
                Ball("Крона",x,.91f,.10f,.70f,.65f,.45f,green);
            }
            if(id=="fires")
            {
                Ball("Огонь",0,.40f,-.15f,.35f,.70f,.20f,amber);
                Ball("Огонь жёлтый",0,.34f,-.22f,.18f,.50f,.13f,yellow);
            }
        }
        private static void Hospital(string id)
        {
            Box("Здание поликлиники",0,.47f,.12f,1.33f,.68f,.61f,white);
            Box("Дверь",.37f,.29f,-.21f,.29f,.30f,.035f,blue);
            Box("Крест вертикальный",-.29f,.66f,-.22f,.12f,.37f,.045f,red);
            Box("Крест горизонтальный",-.29f,.66f,-.23f,.36f,.12f,.045f,red);
            if(id=="flu")
                for(int i=0;i<3;i++)Ball("Вирус",-.50f+i*.5f,.28f,-.42f,.17f,.17f,.17f,green);
        }
        private static void Pipes()
        {
            for(int i=0;i<2;i++)
            {
                Rod("Труба",-.37f+i*.70f,.42f,.10f,.16f,1.05f,steel,new Vector3(90,0,0));
                Rod("Кран",-.37f+i*.70f,.67f,.08f,.23f,.46f,red,new Vector3(0,0,90));
            }
            Ball("Капля",0,.32f,-.38f,.20f,.30f,.17f,blue);
        }
        private static void Power()
        {
            Box("Щиток",0,.55f,.07f,.88f,.95f,.42f,steel);
            Box("Жёлтый индикатор",0,.65f,-.16f,.32f,.41f,.04f,yellow);
            Box("Нижний индикатор",0,.32f,-.16f,.32f,.07f,.04f,dark);
            for(int i=0;i<2;i++)Rod("Кабель",-.48f+i*.96f,.27f,.10f,.05f,.50f,dark,new Vector3(90,0,0));
        }
        private static void Calendar(string id)
        {
            Box("Календарь",0,.60f,.10f,1.05f,.98f,.22f,white);
            Box("Шапка календаря",0,1.0f,-.02f,1.05f,.22f,.04f,red);
            for(int r=0;r<2;r++)for(int c=0;c<3;c++)Box("День",-.30f+c*.3f,.68f-r*.28f,-.02f,.17f,.16f,.025f,stone);
            if(id=="festival")
                Ball("Праздничный шар",.64f,.73f,.1f,.28f,.30f,.23f,yellow);
        }
        private static void Office(string id)
        {
            Box("Офис",0,.54f,.12f,1.3f,.80f,.62f,steel);
            for(int i=0;i<3;i++)Box("Окно",-.4f+i*.4f,.71f,-.21f,.20f,.22f,.035f,blue);
            Box("Вход",0,.32f,-.23f,.27f,.35f,.045f,dark);
            if(id=="layoffs"||id=="closures")
                Box("Закрытая дверь",0,.34f,-.29f,.41f,.13f,.05f,red);
        }
        private static void Package()
        {
            Box("Доставленный груз",0,.44f,.12f,1.03f,.66f,.66f,bark);
            Box("Клейкая лента",0,.75f,.12f,.17f,.05f,.70f,white);
            Box("Этикетка",0,.48f,-.22f,.55f,.28f,.04f,white);
        }
    }
}
