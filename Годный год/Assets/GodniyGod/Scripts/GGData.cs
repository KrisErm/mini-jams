using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GodniyGod
{
    // Data is deliberately kept in one script so that text can be edited quickly during a game jam.
    public sealed class GGEvent
    {
        public string id, title, description;
        public int score, seasonMask, introduced;
        public bool isWeather;
        public GGEvent(string id, string title, string description, int score, int seasonMask, int introduced, bool weather)
        {
            this.id=id; this.title=title; this.description=description; this.score=score;
            this.seasonMask=seasonMask; this.introduced=introduced; isWeather=weather;
        }
        public int GetScore(int season)
        {
            return score - (isWeather && (seasonMask & (1 << season)) == 0 ? 3 : 0);
        }
    }

    public sealed class GGMonthRecord
    {
        public string name;
        public int season, slot, score;
        public List<GGEvent> events = new List<GGEvent>();
        public List<string> consequences = new List<string>();
    }

    public sealed class GGYearRecord
    {
        public int yearIndex;
        public List<GGMonthRecord> months = new List<GGMonthRecord>();
        public int happiness;
        public string grade;
    }

    public static class GGData
    {
        public static readonly string[] Seasons={"ВЕСНА", "ЛЕТО", "ОСЕНЬ", "ЗИМА"};
        public static readonly string[] SeasonResources={"spring", "summer", "autumn", "winter"};
        public static readonly string[][] Months={
            new[]{"Март", "Апрель", "Май"}, new[]{"Июнь", "Июль", "Август"},
            new[]{"Сентябрь", "Октябрь", "Ноябрь"}, new[]{"Декабрь", "Январь", "Февраль"}
        };
        public static readonly string[] YearNames={"2027", "2028", "2029"};
        public static readonly int[] Delivered={12,11,10};
        public const int MonthMaxScore=12;
        public static readonly List<GGEvent> All=new List<GGEvent>();
        private const int Any=15, Spring=1, Summer=2, Autumn=4, Winter=8;

        static GGData()
        {
            // Weather: normal in appropriate seasons, risky if supplied in the wrong one.
            W("sunny", "Солнечные дни", "Много ясных, сухих дней.", 1, Spring|Summer, 0);
            W("warm", "Необычно тепло", "Температура выше сезонной нормы.", 1, Spring|Autumn, 0);
            W("lightSnow", "Небольшой снег", "Спокойный снегопад без последствий.", 0, Autumn|Winter, 0);
            W("snow", "Сильный снегопад", "За ночь выпало очень много снега.", -1, Winter, 0);
            W("rain", "Затяжные дожди", "Дождь почти не прекращается.", -1, Spring|Autumn, 0);
            W("frost", "Сильные морозы", "Температура опустилась ниже -25 °C.", -1, Winter, 0);
            W("thunder", "Грозы", "Несколько сильных гроз подряд.", -1, Spring|Summer, 0);
            W("fog", "Густой туман", "Видимость на дорогах ухудшилась.", -1, Autumn|Winter, 0);
            W("thaw", "Ранняя оттепель", "Снег начал таять раньше срока.", 0, Spring|Winter, 0);
            W("heat", "Аномальная жара", "Температура держится выше +35 °C.", -1, Summer, 0);
            W("hail", "Крупный град", "Град повредил крыши и машины.", -2, Spring|Summer, 0);
            W("blizzard", "Снежная буря", "Сильный ветер и метель.", -2, Winter, 1);
            W("hurricane", "Штормовой ветер", "Порывы ветра валят деревья.", -2, Spring|Autumn, 1);
            W("drought", "Продолжительная засуха", "Дождей не было несколько недель.", -2, Summer, 1);
            E("flood", "Наводнение", "Вода затопила дороги и жилые кварталы.", -3, 1);
            W("ice", "Сильный гололёд", "Дороги и тротуары покрылись льдом.", -2, Winter|Spring, 0);
            W("coldSnap", "Резкое похолодание", "За сутки температура упала на 15 градусов.", -1, Spring|Autumn, 0);
            W("dry", "Сухая ясная погода", "Комфортные дни без осадков.", 1, Summer|Autumn, 0);
            W("mild", "Умеренно тепло", "Погода держится мягкой и комфортной.", 0, Spring|Summer|Autumn, 0);
            W("lightRain", "Небольшие дожди", "Короткие дожди почти не мешают делам.", 0, Spring|Summer|Autumn, 0);
            W("cloudy", "Пасмурные дни", "Небо затянуто, но без серьёзных последствий.", 0, Spring|Autumn|Winter, 0);
            W("clearFrost", "Морозно и ясно", "Холодно, но погода устойчивая и спокойная.", 0, Winter, 0);
            W("variableClouds", "Переменная облачность", "Солнце временами скрывается за облаками.", 0, Any, 0);
            W("seasonalWeather", "Обычная сезонная погода", "Температура и осадки в пределах нормы.", 1, Any, 0);
            W("comfortable", "Комфортная температура", "Ни жары, ни сильного холода.", 1, Any, 0);
            W("quickShowers", "Кратковременные дожди", "Дождь быстро начинается и заканчивается.", 0, Spring|Summer|Autumn, 0);
            W("breeze", "Лёгкий ветер", "Тихая погода с небольшим ветром.", 0, Spring|Summer|Autumn, 0);
            W("coolEvenings", "Прохладные вечера", "Вечером немного прохладнее обычного.", 0, Spring|Summer|Autumn, 0);
            W("winterMild", "Зима без сильных морозов", "Зимняя погода оказалась мягче обычного.", 1, Winter, 0);
            W("steadySnow", "Спокойный снегопад", "Снег идёт без метелей и сильного ветра.", 0, Winter, 0);

            W("storm", "Ливень с подтоплениями", "Несколько улиц оказалось под водой.", -2, Spring|Summer|Autumn, 2);
            W("noSnow", "Бесснежная зима", "Зимой почти не было снега.", 0, Winter, 1);

            // Everyday, social and economic events; no fantasy elements.
            E("bonus", "Выплатили премии", "Работники получили внеплановые премии.", 2, 0);
            E("raise", "Повысили зарплаты", "Заработная плата выросла на 15%.", 2, 0);
            E("vacation", "Добавили неделю отпуска", "Оплачиваемый отпуск стал длиннее.", 2, 0);
            E("holidays", "Дополнительные выходные", "Объявлено несколько оплачиваемых выходных.", 2, 0);
            E("pricesDown", "Снизились цены", "Продукты первой необходимости подешевели.", 2, 0);
            E("park", "Открылся новый парк", "В городе появилось место для прогулок.", 1, 0);
            E("festival", "Городской фестиваль", "Большой праздник с бесплатными мероприятиями.", 1, 0);
            E("harvest", "Хороший урожай", "Фермеры собрали много овощей и фруктов.", 1, 0);
            E("freeTransport", "Бесплатный проезд", "На месяц отменили плату за городской транспорт.", 2, 0);
            E("schoolHoliday", "Школьные каникулы", "Школьникам дали дополнительные дни отдыха.", 1, 0);
            E("newHospital", "Открыли поликлинику", "Медицинская помощь стала доступнее.", 1, 0);
            E("shortWeek", "Сократили рабочую неделю", "Пятница стала дополнительным выходным.", 2, 1);
            E("housing", "Снизили ставки по ипотеке", "Покупка жилья стала немного доступнее.", 1, 1);
            E("museums", "Бесплатные музеи", "Музеи стали бесплатными на целый месяц.", 1, 0);
            E("cityCleanup", "Убрали городские свалки", "Обновили несколько общественных пространств.", 1, 0);
            E("betterTrains", "Запустили новые автобусы", "Общественный транспорт стал ходить чаще.", 1, 0);
            E("yard", "Начали благоустройство двора", "Во дворах обновляют дорожки и скамейки.", 0, 0);
            E("market", "Открылась ярмарка выходного дня", "На площади заработала сезонная ярмарка.", 0, 0);
            E("schedule", "Изменили расписание автобусов", "К новому графику пришлось немного привыкать.", 0, 0);
            E("library", "Обновили районную библиотеку", "Появилось ещё одно спокойное место для досуга.", 0, 0);
            E("streetLight", "Обновили уличное освещение", "В тёмное время суток стало удобнее ходить по улицам.", 1, 0);
            E("busSchedule", "Плановая проверка автобусов", "Транспорт проходит регулярный технический осмотр.", 0, 0);
            E("healthCheck", "Профилактические медосмотры", "В поликлиниках проходит плановая проверка здоровья.", 0, 0);
            E("springCleanup", "Городской субботник", "Жители вместе привели дворы в порядок.", 0, 0);
            E("newCrosswalk", "Появились новые переходы", "Несколько улиц оборудовали удобными переходами.", 1, 0);
            E("weekendMarket", "Фермерская ярмарка", "По выходным открылись сезонные торговые ряды.", 0, 0);
            E("sportsGround", "Обновили спортивную площадку", "Жители получили новое место для занятий спортом.", 1, 0);
            E("exams", "Начались выпускные экзамены", "Школы перешли к привычной экзаменационной поре.", 0, 0);
            E("festivalPrep", "Готовят городской праздник", "Организаторы устанавливают сцену и украшения.", 0, 0);

            E("roadWorks", "Ремонт дорог", "Несколько важных улиц перекрыли на ремонт.", -1, 0);
            E("noHotWater", "Отключили горячую воду", "Плановые работы затянулись на месяц.", -1, 0);
            E("traffic", "Большие пробки", "Дорога на работу занимает вдвое больше времени.", -1, 0);
            E("flu", "Эпидемия гриппа", "Резко выросло число заболевших.", -2, 0);
            E("transport", "Сбой общественного транспорта", "Маршруты отменяются, люди опаздывают.", -2, 0);
            E("power", "Перебои электричества", "В нескольких районах отключают свет.", -2, 0);
            E("utility", "Подорожали коммунальные услуги", "Счета за отопление и воду заметно выросли.", -2, 0);
            E("priceRise", "Резко выросли цены", "Продукты и бытовые товары подорожали.", -2, 0);
            E("salaryDelay", "Задержка зарплат", "Сотрудникам не платят вовремя.", -2, 0);
            E("tax", "Повысили налоги", "Налоговая нагрузка увеличилась.", -2, 0);
            E("phone", "Сбой мобильной связи", "Связь и интернет работают с перебоями.", -1, 0);
            E("water", "Отключение водоснабжения", "Несколько районов остались без воды.", -2, 0);
            E("fires", "Лесные пожары", "Сухая погода привела к пожарам.", -3, 1);
            E("layoffs", "Массовые увольнения", "Крупные предприятия сокращают сотрудников.", -3, 1);
            E("workSat", "Рабочие субботы", "Ввели обязательную шестидневную неделю.", -2, 1);
            E("rent", "Подорожала аренда", "Съёмное жильё стало существенно дороже.", -2, 1);
            E("closedClinic", "Закрылась районная больница", "За медицинской помощью нужно ездить дальше.", -3, 2);
            E("fuel", "Подорожало топливо", "Из-за цен на бензин выросли расходы на транспорт.", -2, 1);
            E("closures", "Закрываются предприятия", "Местные предприятия прекращают работу.", -3, 2);
            E("heating", "Авария на теплосетях", "Несколько кварталов остались без отопления.", -3, 2);
            E("waterLimits", "Ограничения на воду", "Подачу воды по часам ввели из-за засухи.", -2, 2);
            E("longRepairs", "Ремонт затянулся", "Дороги обещали открыть, но снова перенесли срок.", -1, 1);
            E("remote", "Перешли на удалённую работу", "Многим сотрудникам разрешили работать из дома.", 1, 0);
            E("newYearHoliday", "Продлили зимние каникулы", "Новогодние выходные стали дольше.", 1, 0);
            E("bakery", "Открылась пекарня", "На углу снова продают свежий хлеб.", 1, 0);
            E("bookSwap", "Открыли пункт обмена книгами", "Книги можно оставить или забрать бесплатно.", 0, 0);
            E("streetMusic", "Концерт во дворе", "Местные музыканты сыграли небольшой концерт.", 0, 0);
            E("recycling", "Поставили контейнеры для стекла", "Во дворах появились отдельные баки для стекла.", 0, 0);
            E("communityGarden", "Открыли общественный сад", "Жители могут выращивать овощи рядом с домом.", 1, 0);
            E("clinicHours", "Поликлиника работает дольше", "На приём теперь можно попасть и вечером.", 2, 0);
            E("libraryHours", "Библиотеки открыты по выходным", "В читальный зал можно прийти в воскресенье.", 1, 0);
            E("courierDelay", "Задержки на почте", "Посылки приходят на несколько дней позже.", -1, 0);
            E("pedestrianStreet", "Улицу сделали пешеходной", "В центре появилось больше места для прогулок.", 2, 0);
            E("earthquake", "Землетрясение", "После толчков часть зданий закрыли на обследование.", -3, 1);
            E("landslide", "Оползень", "Сползший грунт перекрыл горную дорогу.", -3, 1);
            E("bridgeClosure", "Аварийное закрытие моста", "Через реку приходится ехать в объезд.", -2, 1);
            E("volcano", "Вулкан активизировался", "Пепел накрыл посёлки, часть рейсов отменили.", -3, 2);
            E("damBreach", "Прорвало дамбу", "Низкие районы пришлось эвакуировать.", -4, 2);
            E("blackout", "Авария на подстанции", "Большая часть города осталась без света.", -3, 2);
            E("rockfall", "Камнепад на трассе", "Обломки скалы перекрыли дорогу.", -2, 2);
            E("trafficFine", "Повысили штрафы", "За нарушения на дорогах увеличили штрафы.", -1, 1);
        }

        private static void E(string id, string title, string desc, int score, int year)
        { All.Add(new GGEvent(id,title,desc,score,Any,year,false)); }
        private static void W(string id, string title, string desc, int score, int mask, int year)
        { All.Add(new GGEvent(id,title,desc,score,mask,year,true)); }

        public static string[] GetBoxMonths(int year, int season)
        {
            var m=(string[])Months[season].Clone();
            if(year == 1)
            {
                if(season==1) m[0]="Июль";
                if(season==2) m[1]="Ноябрь";
                if(season==3) m[2]="Январь";
            }
            if(year==2)
            {
                if(season==0) m[1]="Март";
                if(season==1) {m[0]="Июль";m[1]="Июль";}
                if(season==2) m[1]="Ноябрь";
                if(season==3) m[1]="Ноябрь";
            }
            return m;
        }

        // A seeded run gets four distinct batches per year. Rebuilding a season with
        // the same seed gives the same result, including its order on the conveyor.
        public static List<GGEvent> GetDelivery(int year, int season, int runSeed=0)
        {
            var rng=new System.Random(unchecked(runSeed + 10028 + year*917));
            var used=new HashSet<string>();
            List<GGEvent> output=null;
            for(int i=0;i<=season;i++) output=BuildDelivery(year,i,rng,used);
            return output;
        }

        private static readonly string[][] YearTwoIssues={
            new[]{"flood"}, new[]{"earthquake"}, new[]{"landslide"}, new[]{"bridgeClosure"}
        };
        private static readonly string[][] YearThreeIssues={
            new[]{"volcano","blackout"}, new[]{"fires","drought"},
            new[]{"damBreach","rockfall"}, new[]{"blizzard","heating"}
        };
        private static readonly HashSet<string> ScheduledIssues=new HashSet<string>(
            YearTwoIssues.Concat(YearThreeIssues).SelectMany(group=>group));

        private static List<GGEvent> BuildDelivery(int year,int season,System.Random rng,HashSet<string> used)
        {
            var output=new List<GGEvent>();
            var themes=new Dictionary<string,int>();
            int[] targets=year==0 ? new[]{6,4,2} : year==1 ? new[]{3,4,4} : new[]{2,2,6};
            string[] anchor=year==0 ? new string[0] : year==1 ? YearTwoIssues[season] : YearThreeIssues[season];
            foreach(string id in anchor)
            {
                var ev=All.Find(item=>item.id==id);
                AddToDelivery(output,themes,used,ev);
            }
            targets[2]-=anchor.Length;
            for(int tier=0;tier<targets.Length;tier++)
            for(int n=0;n<targets[tier];n++)
            {
                var candidates=All.Where(e=>e.introduced<=year && !output.Contains(e) &&
                    !ScheduledIssues.Contains(e.id) &&
                    (year!=0 || !e.isWeather || (e.seasonMask & (1<<season))!=0) &&
                    DeliveryTier(e,year,season)==tier).ToList();
                // If a small pool ran out, do not repeat current-season items.
                var fresh=candidates.Where(e=>!used.Contains(e.id)).ToList();
                if(fresh.Count!=0) candidates=fresh;
                if(candidates.Count==0) throw new InvalidOperationException("Неполная поставка обстоятельств");
                var weighted=new List<GGEvent>();
                foreach(var e in candidates)
                {
                    bool inSeason=!e.isWeather || (e.seasonMask & (1<<season))!=0;
                    int weight=inSeason ? 9 : year==1 ? 3 : 5;
                    if(themes.TryGetValue(DeliveryTheme(e),out int already)) weight=Math.Max(1,weight/(4*already));
                    for(int w=0;w<weight;w++) weighted.Add(e);
                }
                AddToDelivery(output,themes,used,weighted[rng.Next(weighted.Count)]);
            }

            // Avoid showing four clones of the same visual family on one page.
            var shuffled=new List<GGEvent>();
            var pageThemes=new HashSet<string>();
            // The character can comment on these immediately: they're in the first visible batch.
            if(season==0)
                foreach(string id in anchor)
                {
                    var entry=output.Find(item=>item.id==id);
                    shuffled.Add(entry);output.Remove(entry);pageThemes.Add(DeliveryTheme(entry));
                }
            while(output.Count>0)
            {
                if(shuffled.Count%4==0)pageThemes.Clear();
                var choices=output.Where(e=>!pageThemes.Contains(DeliveryTheme(e))).ToList();
                if(choices.Count==0)choices=output;
                var next=choices[rng.Next(choices.Count)];
                shuffled.Add(next);output.Remove(next);pageThemes.Add(DeliveryTheme(next));
            }
            return shuffled;
        }

        private static int DeliveryTier(GGEvent item,int year,int season)
        {
            int points=item.GetScore(season);
            if(points>0) return 0;
            if(points>= (year==0 ? 0 : -1)) return 1;
            return 2;
        }

        private static void AddToDelivery(List<GGEvent> output,Dictionary<string,int> themes,HashSet<string> used,GGEvent evt)
        {
            output.Add(evt);used.Add(evt.id);
            string theme=DeliveryTheme(evt);
            themes.TryGetValue(theme,out int count);
            themes[theme]=count+1;
        }

        private static string DeliveryTheme(GGEvent item)
        {
            switch(item.id)
            {
                case "sunny":case "warm":case "heat":case "dry":case "drought":case "comfortable":case "winterMild": return "солнце";
                case "snow":case "lightSnow":case "blizzard":case "ice":case "frost":case "coldSnap":case "steadySnow": return "снег";
                case "lightRain":case "rain":case "thunder":case "storm":case "hail":case "hurricane":case "quickShowers": return "дождь";
                case "fog":case "cloudy":case "mild":case "variableClouds":case "seasonalWeather":case "breeze":case "coolEvenings":case "thaw":case "noSnow": return "облака";
                case "traffic":case "transport":case "betterTrains":case "freeTransport":case "fuel":case "trafficFine":case "busSchedule":case "schedule":case "newCrosswalk":return "автобус";
                case "bonus":case "raise":case "pricesDown":case "priceRise":case "salaryDelay":case "utility":case "tax":case "rent":case "housing":return "деньги";
                case "park":case "cityCleanup":case "harvest":case "fires":case "springCleanup":case "sportsGround":return "дерево";
                case "newHospital":case "closedClinic":case "flu":case "healthCheck":case "clinicHours":return "больница";
                case "noHotWater":case "water":case "waterLimits":case "heating":return "трубы";
                case "power":case "phone":case "streetLight":case "blackout":return "электричество";
                case "vacation":case "holidays":case "schoolHoliday":case "shortWeek":case "workSat":case "newYearHoliday":case "festival":case "museums":case "festivalPrep":case "exams":return "календарь";
                case "layoffs":case "closures":case "remote":case "weekendMarket":return "офис";
                case "bookSwap":case "libraryHours":case "library":return "книги";
                case "landslide":case "rockfall":return "камни";
                case "damBreach":case "flood":return "затопление";
                case "roadWorks":case "longRepairs":return "ремонт";
                default: return item.id;
            }
        }

        public static int ScoreMonth(GGMonthRecord month)
        {
            int score=8; // Three neutral circumstances make an ordinary, usable month.
            foreach(var e in month.events) score+=e.GetScore(month.season);
            var ids=new HashSet<string>(month.events.Select(e=>e.id));
            month.consequences.Clear();
            Pair(ids, "heat", "noHotWater", -2, "Жара и отсутствие горячей воды превратили месяц в испытание.", month, ref score);
            Pair(ids, "heat", "water", -2, "В жару отключили воду. Люди остались недовольны.", month, ref score);
            Pair(ids, "drought", "waterLimits", -2, "Засуха вынудила ограничить потребление воды.", month, ref score);
            Pair(ids, "rain", "flood", -2, "Продолжительные дожди усилили наводнение.", month, ref score);
            Pair(ids, "storm", "flood", -2, "Подтопления переросли в серьёзное наводнение.", month, ref score);
            Pair(ids, "snow", "transport", -2, "Снегопад окончательно парализовал транспорт.", month, ref score);
            Pair(ids, "ice", "traffic", -1, "Из-за гололёда пробки стали ещё длиннее.", month, ref score);
            Pair(ids, "raise", "priceRise", -2, "Прибавку к зарплате мгновенно съели новые цены.", month, ref score);
            Pair(ids, "bonus", "salaryDelay", -1, "Премии объявили, но выплатить вовремя не смогли.", month, ref score);
            Pair(ids, "vacation", "layoffs", -2, "После отпуска многим уже некуда возвращаться.", month, ref score);
            Pair(ids, "shortWeek", "workSat", -2, "Четырёхдневку ввели одновременно с рабочими субботами.", month, ref score);
            Pair(ids, "sunny", "holidays", 2, "Солнечная погода пришлась на дополнительные выходные.", month, ref score);
            Pair(ids, "warm", "park", 1, "В новом парке было необычно много посетителей.", month, ref score);
            Pair(ids, "remote", "snow", 1, "Благодаря удалёнке снегопад пережили спокойнее.", month, ref score);
            Pair(ids, "harvest", "pricesDown", 1, "Хороший урожай помог удержать низкие цены.", month, ref score);
            Pair(ids, "power", "heating", -2, "Без света и отопления месяц запомнился надолго.", month, ref score);
            Pair(ids, "roadWorks", "longRepairs", -1, "Ремонт дорог опять не закончили в срок.", month, ref score);
            Pair(ids, "hurricane", "power", -2, "Штормовой ветер оставил районы без электричества.", month, ref score);
            Pair(ids, "earthquake", "power", -1, "После толчков ремонт электросети затянулся.", month, ref score);
            Pair(ids, "damBreach", "water", -1, "Из-за прорыва дамбы восстановление водоснабжения отложили.", month, ref score);
            Pair(ids, "drought", "fires", -2, "Из-за засухи лесные пожары быстро распространились.", month, ref score);
            // Contradictory weather is intentionally not blocked: it is the central joke.
            bool hasHot=ids.Contains("heat")||ids.Contains("sunny");
            bool hasSnow=ids.Contains("snow")||ids.Contains("lightSnow")||ids.Contains("blizzard");
            if(hasHot&&hasSnow){score-=2;month.consequences.Add("В одном месяце одновременно наступили жара и снегопад.");}
            return Mathf.Clamp(score,0,MonthMaxScore);
        }

        private static void Pair(HashSet<string> ids, string a, string b, int delta, string text, GGMonthRecord rec, ref int score)
        { if(ids.Contains(a)&&ids.Contains(b)){score+=delta;rec.consequences.Add(text);} }

        public static int QualityPercent(int points,int months)
        {
            if(months<=0)return 0;
            return Mathf.Clamp(Mathf.RoundToInt(points*100f/(months*MonthMaxScore)),0,100);
        }

        public static void EvaluateYear(GGYearRecord rec)
        {
            // Labels were printed by the factory before the player touched the boxes.
            rec.happiness=QualityPercent(rec.months.Sum(month=>month.score),rec.months.Count);
            rec.grade=rec.happiness>=78?"Годен":rec.happiness>=53?"Условно годен":rec.happiness>=30?"Допущен с замечаниями":"Производственный брак";
        }
    }
}
