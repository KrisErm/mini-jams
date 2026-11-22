using System;
using System.Collections.Generic;

namespace GoodIntentions
{
    [Serializable] public sealed class Story { public Person[] people; public Incident[] events; }
    [Serializable] public sealed class Person
    {
        public string id, name, job, detail, secret, links, clue;
        public int age;
    }
    [Serializable] public sealed class Incident
    {
        public string id, title, time, subject, body, bodyIfDeadAlisa, bodyIfDeadSonya, memo, art, order;
        public Choice[] choices;
    }
    [Serializable] public sealed class Choice
    {
        public string label, intent, hint, requiresRead;
        public int cost, requiresEvidence;
        public Outcome[] outcomes;
    }
    [Serializable] public sealed class Outcome
    {
        public string text, fatalText;
        public string[] when, flags;
        public Change[] changes;
        public int evidence;
        public bool orderFailed;
    }
    [Serializable] public sealed class Change { public string person, kind, note; }
    [Serializable] public sealed class PersonState
    {
        public string id;
        public int health = 2;
        public List<string> notes = new List<string>();
    }
    [Serializable] public sealed class Record
    {
        public string title, choice, report;
        public int shift;
        public bool isFinale;
    }
    [Serializable] public sealed class SaveState
    {
        public int version = 1, step, seals = 3, evidence, finalePage;
        public bool pendingReport, revealAcknowledged;
        public bool introComplete;
        public int introPage, briefedShift;
        // Last expenditure appears in the result, to distinguish approving a decision
        // from spending a resource. Old saves default this value to zero.
        public int lastSealCost;
        // Only failed explicit written orders count. Total breaches persist across shifts.
        // Legacy per-shift field is retained solely for compatibility with old save files.
        public int violationsThisShift, totalViolations;
        // 0, 1, or 3 withheld seals at the beginning of the next shift.
        public int pendingSealReduction;
        // Kept only to migrate saves from FIX 21/22 where all fines were capped at one.
        public bool reduceNextShift, lastOrderFailed;
        public string lastOrderStatus = "";
        public string report = "", lastChoice = "";
        public List<PersonState> people = new List<PersonState>();
        public List<string> read = new List<string>();
        public List<string> flags = new List<string>();
        public List<Record> journal = new List<Record>();
    }

    // Pure C# rules, independent from Unity rendering and input.
    public sealed class GameModel
    {
        public readonly Story Story;
        public SaveState State { get; private set; }
        public const int SealsPerShift = 3;
        public const int FinaleSceneCount = 7;
        public GameModel(Story story, SaveState save = null)
        {
            Story = story;
            if (story == null || story.people == null || story.events == null || story.events.Length == 0)
                throw new ArgumentException("Story is empty");
            State = save ?? new SaveState();
            ValidateSave();
        }
        void ValidateSave()
        {
            if (State.version != 1 || State.step < 0 || State.step > Story.events.Length)
                throw new ArgumentException("Unsupported save version or progress");
            if (State.people == null) State.people = new List<PersonState>();
            if (State.read == null) State.read = new List<string>();
            if (State.flags == null) State.flags = new List<string>();
            if (State.journal == null) State.journal = new List<Record>();
            State.seals = Math.Max(0, Math.Min(SealsPerShift, State.seals));
            State.evidence = Math.Max(0, State.evidence);
            State.lastSealCost = Math.Max(0, Math.Min(SealsPerShift, State.lastSealCost));
            State.finalePage = Math.Max(0, Math.Min(FinaleSceneCount - 1, State.finalePage));
            State.violationsThisShift = Math.Max(0, Math.Min(2, State.violationsThisShift));
            State.totalViolations = Math.Max(0, State.totalViolations);
            State.pendingSealReduction = Math.Max(0, Math.Min(SealsPerShift, State.pendingSealReduction));
            // Load a previous save without discarding a penalty that was already earned.
            if (State.reduceNextShift)
            {
                State.pendingSealReduction = Math.Max(State.pendingSealReduction,
                    State.totalViolations >= 3 ? SealsPerShift : 1);
                State.reduceNextShift = false;
            }
            if (State.lastOrderStatus == null) State.lastOrderStatus = "";
            foreach (Person p in Story.people)
            {
                PersonState ps = State.people.Find(x => x.id == p.id);
                if (ps == null) State.people.Add(new PersonState { id = p.id });
                else
                {
                    ps.health = Math.Max(0, Math.Min(2, ps.health));
                    if (ps.notes == null) ps.notes = new List<string>();
                }
            }
            State.introPage = Math.Max(0, Math.Min(3, State.introPage));
            State.briefedShift = Math.Max(0, Math.Min(4, State.briefedShift));
            // Older saves contain no onboarding fields; never interrupt an already active story.
            if (!State.introComplete && (State.step > 0 || State.pendingReport || State.journal.Count > 0))
            { State.introComplete = true; State.briefedShift = Shift; }
            if (Finished) State.pendingReport = false;
        }
        public PersonState Status(string id) { return State.people.Find(p => p.id == id); }
        public Person Person(string id) { return Array.Find(Story.people, p => p.id == id); }
        public bool Finished { get { return State.step >= Story.events.Length; } }
        public Incident Current { get { return Finished ? null : Story.events[State.step]; } }
        public int Shift { get { return Math.Min(4, State.step / 4 + 1); } }
        public int Dead { get { return State.people.FindAll(p => p.health == 0).Count; } }
        public int Hurt { get { return State.people.FindAll(p => p.health == 1).Count; } }
        public bool Revealed { get { return Dead > 0; } }
        public void Read(string id)
        {
            if (Person(id) != null && !State.read.Contains(id)) State.read.Add(id);
        }
        public bool ChoiceVisible(Choice choice)
        {
            return UnavailableParticipant(choice) == null;
        }

        string UnavailableParticipant(Choice choice)
        {
            Incident incident = Current;
            if (incident == null || incident.choices == null || incident.choices.Length < 3
                || !ReferenceEquals(choice, incident.choices[2])) return null;
            if ((incident.id == "case_07" || incident.id == "case_11") && Matches("dead:nina")) return "Нина";
            if (incident.id == "case_12" && Matches("dead:vera")) return "Вера";
            return null;
        }

        public string BlockReason(Choice choice)
        {
            if (Finished || State.pendingReport) return "Решение уже принято";
            string unavailableParticipant = UnavailableParticipant(choice);
            if (unavailableParticipant != null) return unavailableParticipant + " не может участвовать в этом деле";
            if (!string.IsNullOrEmpty(choice.requiresRead) && !State.read.Contains(choice.requiresRead))
                return "Прочитайте досье: " + Person(choice.requiresRead).name;
            
            if (State.seals < choice.cost) return "Не хватает печатей вмешательства";
            return "";
        }
        public bool Matches(string condition)
        {
            if (string.IsNullOrEmpty(condition)) return true;
            if (condition.StartsWith("!")) return !Matches(condition.Substring(1));
            string[] parts = condition.Split(':');
            if (parts.Length != 2) return false;
            if (parts[0] == "support" && parts[1] == "elena")
            {
                int helpers = 0;
                foreach (string id in new[] { "lev", "pavel", "sonya" })
                {
                    PersonState helper = Status(id);
                    if (helper != null && helper.health == 2) helpers++;
                }
                return helpers >= 2;
            }
            PersonState p = Status(parts[1]);
            switch (parts[0])
            {
                case "hurt": return p != null && p.health == 1;
                case "dead": return p != null && p.health == 0;
                case "well": return p != null && p.health == 2;
                case "alive": return p != null && p.health > 0;
                case "flag": return State.flags.Contains(parts[1]);
                case "read": return State.read.Contains(parts[1]);
                default: return false;
            }
        }
        public bool WillViolateOrder(Choice choice)
        {
            return !string.IsNullOrEmpty(Current != null ? Current.order : "")
                && PreviewOutcome(choice).orderFailed;
        }
        public Outcome PreviewOutcome(Choice choice)
        {
            Incident incident = Current;
            // The two-seal pantry route guarantees evacuation without a new injury.
            if (incident != null && incident.id == "case_09" && incident.choices.Length > 2
                && ReferenceEquals(choice, incident.choices[2]))
                return new Outcome
                {
                    text = "Тимур находит ключ в коробке и открывает выход через кладовую. Все выбираются во двор без новых травм. Неисправный прибор остаётся в задымлённом кафе.",
                    flags = new[] { "shelter" }
                };
            if (incident != null && incident.id == "case_07" && ReferenceEquals(choice, Current.choices[0])
                && Matches("dead:nina") && Matches("hurt:ilya"))
                return new Outcome
                {
                    text = "Илья не может идти впереди из-за больной ноги. Он показывает дорогу из прохода, а двое жильцов по очереди выводят людей через переулок. Никто не пострадал."
                };
            foreach (Outcome outcome in choice.outcomes)
            {
                bool match = true;
                if (outcome.when != null)
                    foreach (string condition in outcome.when) if (!Matches(condition)) { match = false; break; }
                if (match) return outcome;
            }
            throw new InvalidOperationException("No matching outcome for " + choice.label);
        }
        public bool Resolve(int index)
        {
            if (Finished || State.pendingReport || Current.id == "case_16" || index < 0 || index >= Current.choices.Length) return false;
            Choice choice = Current.choices[index];
            if (BlockReason(choice).Length > 0) return false;
            Outcome outcome = PreviewOutcome(choice);
            // Seals are spent only after the player confirms a paid intervention.
            // Approving a free decision costs no seals.
            State.seals -= choice.cost;
            State.lastSealCost = choice.cost;
            bool hasOrder = !string.IsNullOrEmpty(Current.order);
            State.lastOrderFailed = hasOrder && outcome.orderFailed;
            State.lastOrderStatus = "";
            if (hasOrder)
            {
                if (State.lastOrderFailed)
                {
                    State.totalViolations++;
                    State.violationsThisShift++;
                    if (State.totalViolations == 1)
                    {
                        State.lastOrderStatus = "Поручение не выполнено. Первое предупреждение. Выдача печатей в следующей смене не сокращается.";
                    }
                    else if (Shift < 4)
                    {
                        // Each later violation increases the withheld allowance, up to 2.
                        // The punishment applies only upon issuing the NEXT shift's seals.
                        State.pendingSealReduction = State.totalViolations >= 3 ? SealsPerShift : 1;
                        int allowance = SealsPerShift - State.pendingSealReduction;
                        State.lastOrderStatus = "Поручение не выполнено. Нарушений: " + State.totalViolations
                            + ". В следующей смене выдадут " + allowance + " из " + SealsPerShift + " печатей.";
                    }
                    else
                    {
                        State.lastOrderStatus = "Поручение не выполнено. Нарушение № " + State.totalViolations
                            + " внесено в личное дело (последующая смена не предусмотрена).";
                    }
                }
                else State.lastOrderStatus = "Поручение отдела выполнено.";
            }
            // Отмечаем только первую полученную травму за историю. Учитываем и сохранения.
            bool injuryWasPresent = State.people.Exists(p => p.health == 1)
                || State.flags.Contains("boss_first_hurt_seen");
            bool fatalInjuryOccurred = false;
            if (outcome.changes != null)
                foreach (Change change in outcome.changes)
                {
                    PersonState p = Status(change.person);
                    if (p == null) throw new InvalidOperationException("Unknown person: " + change.person);
                    if (p.health == 0) continue; // Death is permanent; later cases cannot resurrect a person.
                    if (change.kind == "kill") p.health = 0;
                    else if (change.kind == "hurt")
                    {
                        if (!injuryWasPresent && p.health == 2)
                        {
                            State.flags.Add("boss_first_hurt_seen");
                            State.flags.Add("boss_first_hurt_" + State.step);
                            injuryWasPresent = true;
                        }
                        if (p.health == 1)
                        {
                            // The death is described by this incident's authored fatal scene,
                            // rather than by a mechanical explanation of health counters.
                            p.health = 0;
                            fatalInjuryOccurred = true;
                            string verb = change.person == "sonya" || change.person == "vera"
                                || change.person == "nina" || change.person == "alisa"
                                || change.person == "elena" ? "Погибла" : "Погиб";
                            p.notes.Add(verb + " во время происшествия «" + Current.title + "».");
                            continue;
                        }
                        p.health = 1;
                    }
                    else if (change.kind == "heal") p.health = 2;
                    else throw new InvalidOperationException("Unknown effect: " + change.kind);
                    p.notes.Add(change.note);
                }
            if (outcome.flags != null)
                foreach (string flag in outcome.flags) if (!State.flags.Contains(flag)) State.flags.Add(flag);
            State.evidence += outcome.evidence;
            string narrative = outcome.text ?? "";
            if (fatalInjuryOccurred)
            {
                // Author a distinct fatal version for every outcome that can wound someone.
                // Do not show "a second injury killed them" or contradictory survival text.
                narrative = !string.IsNullOrEmpty(outcome.fatalText)
                    ? outcome.fatalText
                    : narrative;
            }
            // The displayed choice and the report must name the same rescuer.
            // If Mark died previously, a duty officer takes his place in case 04.
            bool dutyOfficer = Current.id == "case_04" && index == 0 && Matches("dead:mark");
            if (dutyOfficer)
                narrative = narrative.Replace("Марк уже погиб. ", "");
            State.report = narrative;
            State.lastChoice = dutyOfficer ? "Попросить дежурного вывести Елену" : choice.label;
            State.pendingReport = true;
            string journalReport = State.report + (State.lastOrderStatus.Length > 0 ? "\n\n" + State.lastOrderStatus : "");
            State.journal.Add(new Record { title = Current.title, choice = State.lastChoice, report = journalReport, shift = Shift });
            return true;
        }
        public static readonly string[] ElenaHelpers = { "sonya", "lev", "pavel" };
        public List<string> ReadyHelpers()
        {
            var list = new List<string>();
            foreach (string id in ElenaHelpers)
            {
                PersonState ps = Status(id);
                if (ps != null && ps.health == 2) list.Add(id);
            }
            return list;
        }
        public bool ElenaHasRescueTeam { get { return ReadyHelpers().Count >= 2; } }
        public bool ResolveFinale()
        {
            if (Finished || State.pendingReport || Current.id != "case_16" || State.finalePage != FinaleSceneCount - 1) return false;
            // If no ward died until the library fire, the change of wings is a
            // first-time discovery in the epilogue rather than an earlier reveal.
            if (Dead == 0 && !State.flags.Contains("wings_first_dark_at_elena"))
                State.flags.Add("wings_first_dark_at_elena");
            bool team = ElenaHasRescueTeam;
            List<string> helpers = ReadyHelpers();
            string names = "";
            foreach (string id in helpers)
            {
                if (names.Length > 0) names += ", ";
                names += Person(id).name.Split(' ')[0];
            }
            if (team)
            {
                // Only rescuers who were fit to enter the burning archive die. A previously
                // dead or injured character must never appear as an active rescuer.
                foreach (string id in helpers)
                {
                    PersonState ps = Status(id);
                    ps.health = 0;
                    ps.notes.Add((id == "sonya" ? "Погибла" : "Погиб") + " при обрушении в архиве, пытаясь спасти Елену.");
                }
                State.flags.Add("elena_abundance");
                State.report = "После эвакуации посетителей к Елене через запасной выход смогли пройти: " + names + ". "
                    + "Елена и вошедшие за ней погибли при обрушении лестничной площадки.";
            }
            else
            {
                State.flags.Add("elena_shortage");
                State.report = "Пожарные вскрыли заклинившую дверь и вынесли Елену из архива. Спасти её не удалось.";
            }
            PersonState elena = Status("elena");
            elena.health = 0;
            elena.notes.Add(team
                ? "Погибла при обрушении на запасной лестнице библиотечного архива."
                : "Погибла при пожаре в библиотечном архиве.");
            State.lastChoice = "Последняя попытка спасения";
            State.lastSealCost = 0;
            State.lastOrderStatus = "";
            State.lastOrderFailed = false;
            State.pendingReport = true;
            State.journal.Add(new Record { title = Current.title, report = State.report, shift = Shift, isFinale = true });
            return true;
        }
        public void Next()
        {
            if (!State.pendingReport || Finished) return;
            State.pendingReport = false;
            State.step++;
            // Re-issue seals between shifts; violations never remove an already issued seal.
            if (State.step % 4 == 0 && !Finished)
            {
                State.seals = Math.Max(0, SealsPerShift - State.pendingSealReduction);
                State.pendingSealReduction = 0;
                State.reduceNextShift = false;
                State.violationsThisShift = 0;
            }
        }
    }
}
