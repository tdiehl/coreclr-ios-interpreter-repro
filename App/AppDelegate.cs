using RangedTypes;

namespace ConstraintCrashRepro;

[Register ("AppDelegate")]
public class AppDelegate : UIApplicationDelegate {
	public override bool FinishedLaunching (UIApplication application, NSDictionary? launchOptions)
	{
		Console.WriteLine("Repro: calling Generic<Item, Position>.Run...");
		Console.WriteLine($"Repro: result = {Entry.Run()}");
		Console.WriteLine("Repro: finished without crashing.");
		return true;
	}

	public override UISceneConfiguration GetConfiguration (UIApplication application, UISceneSession connectingSceneSession, UISceneConnectionOptions options)
	{
		return new UISceneConfiguration ("Default Configuration", connectingSceneSession.Role);
	}
}
