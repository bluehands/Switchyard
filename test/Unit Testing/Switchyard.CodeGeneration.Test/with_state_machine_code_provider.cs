using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Threading.Tasks;

namespace Switchyard.CodeGeneration.Test;

[TestClass]
public abstract class with_state_machine_code_provider : CodeProviderSpec
{
    protected const string LicenseDotGraph =
        """
        digraph LicenseFlow {
            
          NoLicense;
          NotRegistered_NoDisplayName [label="NotRegistered"];
          DemoRegistered;
          CommunityRegistered;    
          Full;
            
          NoLicense -> NotRegistered_NoDisplayName [label="DemoLicenseFound"];
          NoLicense -> Full [label="FullLicenseFound"];
          
          NotRegistered_NoDisplayName -> Full [label="FullLicenseFound"];
          NotRegistered_NoDisplayName -> NotRegistered_NoDisplayName [label="DemoLicenseFound"];
          NotRegistered_NoDisplayName -> DemoRegistered [label="DemoRegistered"];
          
          DemoRegistered -> CommunityRegistered [label="CommunityRegistered"];
          DemoRegistered -> DemoRegistered [label="DemoLicenseFound"];
          DemoRegistered -> Full [label="FullLicenseFound"];
          
          Full -> Full [label="FullLicenseFound"];
          
          CommunityRegistered -> Full [label="FullLicenseFound"];
          CommunityRegistered -> CommunityRegistered [label="DemoLicenseFound"];
        }
        """;

    protected override async Task Refactor(AdhocWorkspace workspace, Document document, SyntaxNode root)
    {
        var updatedDoc = await StateMachineCodeProvider.GenerateStateMachine(
                document, GetDotFile, CancellationToken.None)
            .ConfigureAwait(false);
        workspace.TryApplyChanges(updatedDoc.Project.Solution);
    }

    protected abstract (string dotFileName, string fileContent) GetDotFile();
}

[TestClass]
public class When_generating_state_machine_for_simple_dot_graph : with_state_machine_code_provider
{
    protected override string WithSource() => "namespace StateMachineSpecs {}";

    protected override (string dotFileName, string fileContent) GetDotFile() => ("LicenseState.dot", LicenseDotGraph);
}

[TestClass]
public class When_generating_state_machine_for_simple_dot_graph_with_existing_code : with_state_machine_code_provider
{
    protected override string WithSource() =>
        """
        using System;
        using System.Threading.Tasks;

        namespace StateMachineSpecs
        {
            public abstract class LicenseState
            {
                public static readonly LicenseState NoLicense = new NoLicense_();
                public static readonly LicenseState NotRegistered = new NotRegistered_();
                public static readonly LicenseState DemoRegistered = new DemoRegistered_();
                public static readonly LicenseState CommunityRegistered = new CommunityRegistered_();
                public static readonly LicenseState Full = new Full_();
        
                public class NoLicense_ : LicenseState
                {
                    public NoLicense_() : base(UnionCases.NoLicense)
                    {
                    }
                    #region NoLicense -> NotRegistered [label="DemoLicenseFound"]
                    public LicenseState.NotRegistered_ DemoLicenseFound() 
                    {
                        var x = "Still not registered";
                        return new LicenseState.NotRegistered_();
                    }
                    public LicenseState.NotRegistered_ DemoLicenseFound(LicenseTrigger.DemoLicenseFound_ parameters) => DemoLicenseFound();
                    #endregion
                    #region NoLicense -> Full [label="FullLicenseFound"]
                    public LicenseState.Full_ FullLicenseFound() => new LicenseState.Full_();
                    public LicenseState.Full_ FullLicenseFound(LicenseTrigger.FullLicenseFound_ parameters) => FullLicenseFound();
                    #endregion
                }
        
                public class NotRegistered_ : LicenseState
                {
                    public NotRegistered_() : base(UnionCases.NotRegistered)
                    {
                    }
                    #region NotRegistered -> Full [label="FullLicenseFound"]
                    public LicenseState.Full_ FullLicenseFound() => new LicenseState.Full_();
                    public LicenseState.Full_ FullLicenseFound(LicenseTrigger.FullLicenseFound_ parameters) => FullLicenseFound();
                    #endregion
                    #region NotRegistered -> NotRegistered [label="DemoLicenseFound"]
                    public LicenseState.NotRegistered_ DemoLicenseFound() => new LicenseState.NotRegistered_();
                    public LicenseState.NotRegistered_ DemoLicenseFound(LicenseTrigger.DemoLicenseFound_ parameters) => DemoLicenseFound();
                    #endregion
                    #region NotRegistered -> DemoRegistered [label="DemoRegistered"]
                    public LicenseState.DemoRegistered_ DemoRegistered() => new LicenseState.DemoRegistered_();
                    public LicenseState.DemoRegistered_ DemoRegistered(LicenseTrigger.DemoRegistered_ parameters) => DemoRegistered();
                    #endregion
                }
        
                public class DemoRegistered_ : LicenseState
                {
                    public DemoRegistered_() : base(UnionCases.DemoRegistered)
                    {
                    }
                    #region DemoRegistered -> CommunityRegistered [label="CommunityRegistered"]
                    public LicenseState.CommunityRegistered_ CommunityRegistered() => new LicenseState.CommunityRegistered_();
                    public LicenseState.CommunityRegistered_ CommunityRegistered(LicenseTrigger.CommunityRegistered_ parameters) => CommunityRegistered();
                    #endregion
                    #region DemoRegistered -> DemoRegistered [label="DemoLicenseFound"]
                    public LicenseState.DemoRegistered_ DemoLicenseFound() => new LicenseState.DemoRegistered_();
                    public LicenseState.DemoRegistered_ DemoLicenseFound(LicenseTrigger.DemoLicenseFound_ parameters) => DemoLicenseFound();
                    #endregion
                    #region DemoRegistered -> Full [label="FullLicenseFound"]
                    public LicenseState.Full_ FullLicenseFound() => new LicenseState.Full_();
                    public LicenseState.Full_ FullLicenseFound(LicenseTrigger.FullLicenseFound_ parameters) => FullLicenseFound();
                    #endregion
                }
        
                public class CommunityRegistered_ : LicenseState
                {
                    public CommunityRegistered_() : base(UnionCases.CommunityRegistered)
                    {
                    }
                    #region CommunityRegistered -> Full [label="FullLicenseFound"]
                    public LicenseState.Full_ FullLicenseFound() => new LicenseState.Full_();
                    public LicenseState.Full_ FullLicenseFound(LicenseTrigger.FullLicenseFound_ parameters) => FullLicenseFound();
                    #endregion
                    #region CommunityRegistered -> CommunityRegistered [label="DemoLicenseFound"]
                    public LicenseState.CommunityRegistered_ DemoLicenseFound() => new LicenseState.CommunityRegistered_();
                    public LicenseState.CommunityRegistered_ DemoLicenseFound(LicenseTrigger.DemoLicenseFound_ parameters) => DemoLicenseFound();
                    #endregion
                }
        
                public class Full_ : LicenseState
                {
                    public Full_() : base(UnionCases.Full)
                    {
                    }
                    #region Full -> Full [label="FullLicenseFound"]
                    public LicenseState.Full_ FullLicenseFound() => new LicenseState.Full_();
                    public LicenseState.Full_ FullLicenseFound(LicenseTrigger.FullLicenseFound_ parameters) => FullLicenseFound();
                    #endregion
                }
        
                internal enum UnionCases
                {
                    NoLicense,
                    NotRegistered,
                    DemoRegistered,
                    CommunityRegistered,
                    Full
                }
        
                internal UnionCases UnionCase { get; }
                LicenseState(UnionCases unionCase) => UnionCase = unionCase;
        
                public override string ToString() => Enum.GetName(typeof(UnionCases), UnionCase) ?? UnionCase.ToString();
                bool Equals(LicenseState other) => UnionCase == other.UnionCase;
        
                public override bool Equals(object obj)
                {
                    if (ReferenceEquals(null, obj)) return false;
                    if (ReferenceEquals(this, obj)) return true;
                    if (obj.GetType() != GetType()) return false;
                    return Equals((LicenseState)obj);
                }
        
                public override int GetHashCode() => (int)UnionCase;
            }
        
            public abstract class LicenseTrigger
            {
                public static readonly LicenseTrigger DemoLicenseFound = new DemoLicenseFound_();
                public static readonly LicenseTrigger FullLicenseFound = new FullLicenseFound_();
                public static readonly LicenseTrigger DemoRegistered = new DemoRegistered_();
                public static readonly LicenseTrigger CommunityRegistered = new CommunityRegistered_();
        
                public class DemoLicenseFound_ : LicenseTrigger
                {
                    public DemoLicenseFound_() : base(UnionCases.DemoLicenseFound)
                    {
                    }
                }
        
                public class FullLicenseFound_ : LicenseTrigger
                {
                    public FullLicenseFound_() : base(UnionCases.FullLicenseFound)
                    {
                    }
                }
        
                public class DemoRegistered_ : LicenseTrigger
                {
                    public DemoRegistered_() : base(UnionCases.DemoRegistered)
                    {
                    }
                }
        
                public class CommunityRegistered_ : LicenseTrigger
                {
                    public CommunityRegistered_() : base(UnionCases.CommunityRegistered)
                    {
                    }
                }
        
                internal enum UnionCases
                {
                    DemoLicenseFound,
                    FullLicenseFound,
                    DemoRegistered,
                    CommunityRegistered
                }
        
                internal UnionCases UnionCase { get; }
                LicenseTrigger(UnionCases unionCase) => UnionCase = unionCase;
        
                public override string ToString() => Enum.GetName(typeof(UnionCases), UnionCase) ?? UnionCase.ToString();
                bool Equals(LicenseTrigger other) => UnionCase == other.UnionCase;
        
                public override bool Equals(object obj)
                {
                    if (ReferenceEquals(null, obj)) return false;
                    if (ReferenceEquals(this, obj)) return true;
                    if (obj.GetType() != GetType()) return false;
                    return Equals((LicenseTrigger)obj);
                }
        
                public override int GetHashCode() => (int)UnionCase;
            }
        
            public abstract class LicenseTransitionResult
            {
            }
        
            public class LicenseTransition : LicenseTransitionResult
            {
                public LicenseState Source { get; }
                public LicenseState Destination { get; }
                public LicenseTrigger Trigger { get; }
        
                public LicenseTransition(LicenseState source, LicenseState destination, LicenseTrigger trigger)
                {
                    Source = source; Destination = destination; Trigger = trigger;
                }
            }
        
            public class LicenseInvalidTrigger : LicenseTransitionResult
            {
                public LicenseState Source { get; }
                public LicenseTrigger Trigger { get; }
        
                public LicenseInvalidTrigger(LicenseState source, LicenseTrigger trigger)
                {
                    Source = source; Trigger = trigger;
                }
            }
        
            public static class LicenseExtension
            {
                public static LicenseState Apply(this LicenseState license, LicenseTrigger parameter)
                {
                    switch (license.UnionCase)
                    {
                        case LicenseState.UnionCases.NoLicense:
                            {
                                switch (parameter.UnionCase)
                                {
                                    case LicenseTrigger.UnionCases.DemoLicenseFound:
                                        return ((LicenseState.NoLicense_)license).DemoLicenseFound((LicenseTrigger.DemoLicenseFound_)parameter);
                                    case LicenseTrigger.UnionCases.FullLicenseFound:
                                        return ((LicenseState.NoLicense_)license).FullLicenseFound((LicenseTrigger.FullLicenseFound_)parameter);
                                    default:
                                        return license;
                                }
                            }
        
                        case LicenseState.UnionCases.NotRegistered:
                            {
                                switch (parameter.UnionCase)
                                {
                                    case LicenseTrigger.UnionCases.FullLicenseFound:
                                        return ((LicenseState.NotRegistered_)license).FullLicenseFound((LicenseTrigger.FullLicenseFound_)parameter);
                                    case LicenseTrigger.UnionCases.DemoLicenseFound:
                                        return ((LicenseState.NotRegistered_)license).DemoLicenseFound((LicenseTrigger.DemoLicenseFound_)parameter);
                                    case LicenseTrigger.UnionCases.DemoRegistered:
                                        return ((LicenseState.NotRegistered_)license).DemoRegistered((LicenseTrigger.DemoRegistered_)parameter);
                                    default:
                                        return license;
                                }
                            }
        
                        case LicenseState.UnionCases.DemoRegistered:
                            {
                                switch (parameter.UnionCase)
                                {
                                    case LicenseTrigger.UnionCases.CommunityRegistered:
                                        return ((LicenseState.DemoRegistered_)license).CommunityRegistered((LicenseTrigger.CommunityRegistered_)parameter);
                                    case LicenseTrigger.UnionCases.DemoLicenseFound:
                                        return ((LicenseState.DemoRegistered_)license).DemoLicenseFound((LicenseTrigger.DemoLicenseFound_)parameter);
                                    case LicenseTrigger.UnionCases.FullLicenseFound:
                                        return ((LicenseState.DemoRegistered_)license).FullLicenseFound((LicenseTrigger.FullLicenseFound_)parameter);
                                    default:
                                        return license;
                                }
                            }
        
                        case LicenseState.UnionCases.CommunityRegistered:
                            {
                                switch (parameter.UnionCase)
                                {
                                    case LicenseTrigger.UnionCases.FullLicenseFound:
                                        return ((LicenseState.CommunityRegistered_)license).FullLicenseFound((LicenseTrigger.FullLicenseFound_)parameter);
                                    case LicenseTrigger.UnionCases.DemoLicenseFound:
                                        return ((LicenseState.CommunityRegistered_)license).DemoLicenseFound((LicenseTrigger.DemoLicenseFound_)parameter);
                                    default:
                                        return license;
                                }
                            }
        
                        case LicenseState.UnionCases.Full:
                            {
                                switch (parameter.UnionCase)
                                {
                                    case LicenseTrigger.UnionCases.FullLicenseFound:
                                        return ((LicenseState.Full_)license).FullLicenseFound((LicenseTrigger.FullLicenseFound_)parameter);
                                    default:
                                        return license;
                                }
                            }
        
                        default:
                            throw new ArgumentException($"Unknown type implementing LicenseState: {license.GetType().Name}");
                    }
                }
        
                public static LicenseTransitionResult DoTransition(this LicenseState license, LicenseTrigger parameter)
                {
                    switch (license.UnionCase)
                    {
                        case LicenseState.UnionCases.NoLicense:
                            {
                                switch (parameter.UnionCase)
                                {
                                    case LicenseTrigger.UnionCases.DemoLicenseFound:
                                        return new LicenseTransition(license, ((LicenseState.NoLicense_)license).DemoLicenseFound((LicenseTrigger.DemoLicenseFound_)parameter), parameter);
                                    case LicenseTrigger.UnionCases.FullLicenseFound:
                                        return new LicenseTransition(license, ((LicenseState.NoLicense_)license).FullLicenseFound((LicenseTrigger.FullLicenseFound_)parameter), parameter);
                                    default:
                                        return new LicenseInvalidTrigger(license, parameter);
                                }
                            }
        
                        case LicenseState.UnionCases.NotRegistered:
                            {
                                switch (parameter.UnionCase)
                                {
                                    case LicenseTrigger.UnionCases.FullLicenseFound:
                                        return new LicenseTransition(license, ((LicenseState.NotRegistered_)license).FullLicenseFound((LicenseTrigger.FullLicenseFound_)parameter), parameter);
                                    case LicenseTrigger.UnionCases.DemoLicenseFound:
                                        return new LicenseTransition(license, ((LicenseState.NotRegistered_)license).DemoLicenseFound((LicenseTrigger.DemoLicenseFound_)parameter), parameter);
                                    case LicenseTrigger.UnionCases.DemoRegistered:
                                        return new LicenseTransition(license, ((LicenseState.NotRegistered_)license).DemoRegistered((LicenseTrigger.DemoRegistered_)parameter), parameter);
                                    default:
                                        return new LicenseInvalidTrigger(license, parameter);
                                }
                            }
        
                        case LicenseState.UnionCases.DemoRegistered:
                            {
                                switch (parameter.UnionCase)
                                {
                                    case LicenseTrigger.UnionCases.CommunityRegistered:
                                        return new LicenseTransition(license, ((LicenseState.DemoRegistered_)license).CommunityRegistered((LicenseTrigger.CommunityRegistered_)parameter), parameter);
                                    case LicenseTrigger.UnionCases.DemoLicenseFound:
                                        return new LicenseTransition(license, ((LicenseState.DemoRegistered_)license).DemoLicenseFound((LicenseTrigger.DemoLicenseFound_)parameter), parameter);
                                    case LicenseTrigger.UnionCases.FullLicenseFound:
                                        return new LicenseTransition(license, ((LicenseState.DemoRegistered_)license).FullLicenseFound((LicenseTrigger.FullLicenseFound_)parameter), parameter);
                                    default:
                                        return new LicenseInvalidTrigger(license, parameter);
                                }
                            }
        
                        case LicenseState.UnionCases.CommunityRegistered:
                            {
                                switch (parameter.UnionCase)
                                {
                                    case LicenseTrigger.UnionCases.FullLicenseFound:
                                        return new LicenseTransition(license, ((LicenseState.CommunityRegistered_)license).FullLicenseFound((LicenseTrigger.FullLicenseFound_)parameter), parameter);
                                    case LicenseTrigger.UnionCases.DemoLicenseFound:
                                        return new LicenseTransition(license, ((LicenseState.CommunityRegistered_)license).DemoLicenseFound((LicenseTrigger.DemoLicenseFound_)parameter), parameter);
                                    default:
                                        return new LicenseInvalidTrigger(license, parameter);
                                }
                            }
        
                        case LicenseState.UnionCases.Full:
                            {
                                switch (parameter.UnionCase)
                                {
                                    case LicenseTrigger.UnionCases.FullLicenseFound:
                                        return new LicenseTransition(license, ((LicenseState.Full_)license).FullLicenseFound((LicenseTrigger.FullLicenseFound_)parameter), parameter);
                                    default:
                                        return new LicenseInvalidTrigger(license, parameter);
                                }
                            }
        
                        default:
                            throw new ArgumentException($"Unknown type implementing LicenseState: {license.GetType().Name}");
                    }
                }
        
                public static T Match<T>(this LicenseState license, Func<LicenseState.NoLicense_, T> noLicense, Func<LicenseState.NotRegistered_, T> notRegistered, Func<LicenseState.DemoRegistered_, T> demoRegistered, Func<LicenseState.CommunityRegistered_, T> communityRegistered, Func<LicenseState.Full_, T> full)
                {
                    switch (license.UnionCase)
                    {
                        case LicenseState.UnionCases.NoLicense:
                            return noLicense((LicenseState.NoLicense_)license);
                        case LicenseState.UnionCases.NotRegistered:
                            return notRegistered((LicenseState.NotRegistered_)license);
                        case LicenseState.UnionCases.DemoRegistered:
                            return demoRegistered((LicenseState.DemoRegistered_)license);
                        case LicenseState.UnionCases.CommunityRegistered:
                            return communityRegistered((LicenseState.CommunityRegistered_)license);
                        case LicenseState.UnionCases.Full:
                            return full((LicenseState.Full_)license);
                        default:
                            throw new ArgumentException($"Unknown type derived from LicenseState: {license.GetType().Name}");
                    }
                }
        
                public static async Task<T> Match<T>(this LicenseState license, Func<LicenseState.NoLicense_, Task<T>> noLicense, Func<LicenseState.NotRegistered_, Task<T>> notRegistered, Func<LicenseState.DemoRegistered_, Task<T>> demoRegistered, Func<LicenseState.CommunityRegistered_, Task<T>> communityRegistered, Func<LicenseState.Full_, Task<T>> full)
                {
                    switch (license.UnionCase)
                    {
                        case LicenseState.UnionCases.NoLicense:
                            return await noLicense((LicenseState.NoLicense_)license).ConfigureAwait(false);
                        case LicenseState.UnionCases.NotRegistered:
                            return await notRegistered((LicenseState.NotRegistered_)license).ConfigureAwait(false);
                        case LicenseState.UnionCases.DemoRegistered:
                            return await demoRegistered((LicenseState.DemoRegistered_)license).ConfigureAwait(false);
                        case LicenseState.UnionCases.CommunityRegistered:
                            return await communityRegistered((LicenseState.CommunityRegistered_)license).ConfigureAwait(false);
                        case LicenseState.UnionCases.Full:
                            return await full((LicenseState.Full_)license).ConfigureAwait(false);
                        default:
                            throw new ArgumentException($"Unknown type derived from LicenseState: {license.GetType().Name}");
                    }
                }
        
                public static async Task<T> Match<T>(this Task<LicenseState> license, Func<LicenseState.NoLicense_, T> noLicense, Func<LicenseState.NotRegistered_, T> notRegistered, Func<LicenseState.DemoRegistered_, T> demoRegistered, Func<LicenseState.CommunityRegistered_, T> communityRegistered, Func<LicenseState.Full_, T> full) => (await license.ConfigureAwait(false)).Match(noLicense, notRegistered, demoRegistered, communityRegistered, full);
                public static async Task<T> Match<T>(this Task<LicenseState> license, Func<LicenseState.NoLicense_, Task<T>> noLicense, Func<LicenseState.NotRegistered_, Task<T>> notRegistered, Func<LicenseState.DemoRegistered_, Task<T>> demoRegistered, Func<LicenseState.CommunityRegistered_, Task<T>> communityRegistered, Func<LicenseState.Full_, Task<T>> full) => await(await license.ConfigureAwait(false)).Match(noLicense, notRegistered, demoRegistered, communityRegistered, full).ConfigureAwait(false);
        
                public static T Match<T>(this LicenseTrigger parameter, Func<LicenseTrigger.DemoLicenseFound_, T> demoLicenseFound, Func<LicenseTrigger.FullLicenseFound_, T> fullLicenseFound, Func<LicenseTrigger.DemoRegistered_, T> demoRegistered, Func<LicenseTrigger.CommunityRegistered_, T> communityRegistered)
                {
                    switch (parameter.UnionCase)
                    {
                        case LicenseTrigger.UnionCases.DemoLicenseFound:
                            return demoLicenseFound((LicenseTrigger.DemoLicenseFound_)parameter);
                        case LicenseTrigger.UnionCases.FullLicenseFound:
                            return fullLicenseFound((LicenseTrigger.FullLicenseFound_)parameter);
                        case LicenseTrigger.UnionCases.DemoRegistered:
                            return demoRegistered((LicenseTrigger.DemoRegistered_)parameter);
                        case LicenseTrigger.UnionCases.CommunityRegistered:
                            return communityRegistered((LicenseTrigger.CommunityRegistered_)parameter);
                        default:
                            throw new ArgumentException($"Unknown type derived from LicenseTrigger: {parameter.GetType().Name}");
                    }
                }
        
                public static async Task<T> Match<T>(this LicenseTrigger parameter, Func<LicenseTrigger.DemoLicenseFound_, Task<T>> demoLicenseFound, Func<LicenseTrigger.FullLicenseFound_, Task<T>> fullLicenseFound, Func<LicenseTrigger.DemoRegistered_, Task<T>> demoRegistered, Func<LicenseTrigger.CommunityRegistered_, Task<T>> communityRegistered)
                {
                    switch (parameter.UnionCase)
                    {
                        case LicenseTrigger.UnionCases.DemoLicenseFound:
                            return await demoLicenseFound((LicenseTrigger.DemoLicenseFound_)parameter).ConfigureAwait(false);
                        case LicenseTrigger.UnionCases.FullLicenseFound:
                            return await fullLicenseFound((LicenseTrigger.FullLicenseFound_)parameter).ConfigureAwait(false);
                        case LicenseTrigger.UnionCases.DemoRegistered:
                            return await demoRegistered((LicenseTrigger.DemoRegistered_)parameter).ConfigureAwait(false);
                        case LicenseTrigger.UnionCases.CommunityRegistered:
                            return await communityRegistered((LicenseTrigger.CommunityRegistered_)parameter).ConfigureAwait(false);
                        default:
                            throw new ArgumentException($"Unknown type derived from LicenseTrigger: {parameter.GetType().Name}");
                    }
                }
        
                public static async Task<T> Match<T>(this Task<LicenseTrigger> parameter, Func<LicenseTrigger.DemoLicenseFound_, T> demoLicenseFound, Func<LicenseTrigger.FullLicenseFound_, T> fullLicenseFound, Func<LicenseTrigger.DemoRegistered_, T> demoRegistered, Func<LicenseTrigger.CommunityRegistered_, T> communityRegistered) => (await parameter.ConfigureAwait(false)).Match(demoLicenseFound, fullLicenseFound, demoRegistered, communityRegistered);
                public static async Task<T> Match<T>(this Task<LicenseTrigger> parameter, Func<LicenseTrigger.DemoLicenseFound_, Task<T>> demoLicenseFound, Func<LicenseTrigger.FullLicenseFound_, Task<T>> fullLicenseFound, Func<LicenseTrigger.DemoRegistered_, Task<T>> demoRegistered, Func<LicenseTrigger.CommunityRegistered_, Task<T>> communityRegistered) => await(await parameter.ConfigureAwait(false)).Match(demoLicenseFound, fullLicenseFound, demoRegistered, communityRegistered).ConfigureAwait(false);
            }
        }
        """;

    protected override (string dotFileName, string fileContent) GetDotFile() => ("License.dot", LicenseDotGraph);
}