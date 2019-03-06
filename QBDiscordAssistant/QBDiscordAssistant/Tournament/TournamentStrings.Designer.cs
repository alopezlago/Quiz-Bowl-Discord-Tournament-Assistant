﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace QBDiscordAssistant.Tournament {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "15.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class TournamentStrings {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal TournamentStrings() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("QBDiscordAssistant.Tournament.TournamentStrings", typeof(TournamentStrings).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Add a list of comma-separated team names. If the team name has a comma, use another comma to escape it (like ,,). You can add a maximum of {0} teams..
        /// </summary>
        public static string AddListCommaSeparatedTeams {
            get {
                return ResourceManager.GetString("AddListCommaSeparatedTeams", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Add Readers.
        /// </summary>
        public static string AddReaders {
            get {
                return ResourceManager.GetString("AddReaders", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Add Teams.
        /// </summary>
        public static string AddTeams {
            get {
                return ResourceManager.GetString("AddTeams", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to All tournament channels and roles removed. Tournament &apos;{0}&apos; is now finished..
        /// </summary>
        public static string AllTournamentChannelsRolesRemoved {
            get {
                return ResourceManager.GetString("AllTournamentChannelsRolesRemoved", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The tournament &apos;{0}&apos; couldn&apos;t be moved from pending to current. Try again later..
        /// </summary>
        public static string CannotMoveTournamentFromPending {
            get {
                return ResourceManager.GetString("CannotMoveTournamentFromPending", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Initializing the schedule. Channels and roles will be set up next..
        /// </summary>
        public static string InitializingSchedule {
            get {
                return ResourceManager.GetString("InitializingSchedule", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to List the mentions of all of the readers. For example, &apos;@Reader_1 @Reader_2 @Reader_3&apos;. If you forgot a reader, you can still use !addReaders during the add teams phase..
        /// </summary>
        public static string ListMentionsOfAllReaders {
            get {
                return ResourceManager.GetString("ListMentionsOfAllReaders", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Must have more than 1 team. Count: {0}.
        /// </summary>
        public static string MustHaveMoreThanOneTeam {
            get {
                return ResourceManager.GetString("MustHaveMoreThanOneTeam", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Must have a reader..
        /// </summary>
        public static string MustHaveReader {
            get {
                return ResourceManager.GetString("MustHaveReader", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to No current tournament is running..
        /// </summary>
        public static string NoCurrentTournamentRunning {
            get {
                return ResourceManager.GetString("NoCurrentTournamentRunning", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to roundRobins must be positive. Value: {0}.
        /// </summary>
        public static string RoundRobinsMustBePositive {
            get {
                return ResourceManager.GetString("RoundRobinsMustBePositive", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Set the number of round robins.
        /// </summary>
        public static string SetNumberRoundRobins {
            get {
                return ResourceManager.GetString("SetNumberRoundRobins", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Setting up the tournament.
        /// </summary>
        public static string SettingUpTournament {
            get {
                return ResourceManager.GetString("SettingUpTournament", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Specify the number of round-robin rounds as an integer (from 1 to {0})..
        /// </summary>
        public static string SpecifyNumberRoundRobins {
            get {
                return ResourceManager.GetString("SpecifyNumberRoundRobins", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The tournament &apos;{0}&apos; is already running. Use !end to stop it..
        /// </summary>
        public static string TournamentAlreadyRunning {
            get {
                return ResourceManager.GetString("TournamentAlreadyRunning", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to A tournament with the name &apos;{0}&apos; cannot be found..
        /// </summary>
        public static string TournamentCannotBeFound {
            get {
                return ResourceManager.GetString("TournamentCannotBeFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Tournament Completed.
        /// </summary>
        public static string TournamentCompleted {
            get {
                return ResourceManager.GetString("TournamentCompleted", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Tournament Started.
        /// </summary>
        public static string TournamentStarted {
            get {
                return ResourceManager.GetString("TournamentStarted", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Tournament &apos;{0}&apos; has started. Go to your first round room and follow the instructions..
        /// </summary>
        public static string TournamentStartedDirections {
            get {
                return ResourceManager.GetString("TournamentStartedDirections", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unable to get access to the current tournament. Try again later..
        /// </summary>
        public static string UnableAccessCurrentTournament {
            get {
                return ResourceManager.GetString("UnableAccessCurrentTournament", resourceCulture);
            }
        }
    }
}
