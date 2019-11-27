# Switchyard
Roslyn based refactorings to support functional constructs in C# with less effort

## Refactorings

### Expand enum to union type

Imagine classes representing an ftp connection configuration like that:

```csharp

public enum ConnectionType
{
	UserNameAndPassword,
	KeyFile
}

public class SFtpConfig
{
	public ConnectionType ConnectionType { get; }
	public string User { get; }
	public string Password { get; }
	public string PathToKeyFile { get; }

	public SFtpConfig(ConnectionType connectionType, string user, string password, string pathToKeyFile)
	{
		ConnectionType = connectionType;
		User = user;
		Password = password;
		PathToKeyFile = pathToKeyFile;
	}
}

/// ......

/// ......

public class Consumer
{
	public void Connect(SFtpConfig config)
	{
		switch (config.ConnectionType)
		{
			case ConnectionType.UserNameAndPassword:
				ConnectWithPassword(config.User, config.Password);
				break;
			case ConnectionType.KeyFile:
				ConnectWithKeyFile(config.User, config.PathToKeyFile);
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}
	}
	
	FtpConnection ConnectWithKeyFile(string argUser, string argKeyFilePath) => throw new NotImplementedException();

	FtpConnection ConnectWithPassword(string withPasswordUser, string withPasswordPassword) => throw new NotImplementedException();
}

```

Problem with the config class is that every consumer has to switch on the `ConnectionType` property and than guess which properties on SFtpConfig are relevant in his case. First problem here, the guess could be wrong and types of all 'eventual properties' on SFtpConfig have to allow empty or null values. Second problem is common to all enums: An extension of the enum won't be visible to consumers at compile time. So a third option introduced might lead to ArgumentOutOfRange exceptions at runtime.

One solution to the problem is so called union types or discriminated unions, basically types that can be on thing or an other. Those types are supported natively in F# an other functional languages. In C# they have to be implemented by hand, and that is where the refactoring comes in.

Start with an enum like that:

```csharp

public enum SFtpConfig
{
	Password,
	KeyFile
}

```

As soon as the cursor is inside of an enum declaration, light bulb action 'Expand enum to untion type' is offered, which will change the enum declaration to the following code (equality and internal members ommitted):

```csharp

public abstract class SFtpConfig
{
	public static readonly SFtpConfig Password = new Password_();
	public static readonly SFtpConfig KeyFile = new KeyFile_();
	public class Password_ : SFtpConfig
	{
		public Password_() : base(Ids.Password)
		{
		}
	}

	public class KeyFile_ : SFtpConfig
	{
		public KeyFile_() : base(Ids.KeyFile)
		{
		}
	}
	
	// equality and internal members ....
}

// match helper ....

```

One can can add properties to the union case classes and intialize them from the constructs. Just run the refactoring again and static initializers will be adapted accordingly:

```csharp

public abstract class SFtpConfig
{
	public static readonly SFtpConfig Password = new Password_();	
	public static readonly SFtpConfig KeyFile = new KeyFile_();
	
	public class Password_ : SFtpConfig
	{
		public string Password { get; }

		public Password_(string user, string password) : base(Ids.Password, user) => Password = password;
	}

	public class KeyFile_ : SFtpConfig
	{
		public string KeyFilePath { get; }

		public KeyFile_(string user, string keyFilePath) : base(Ids.KeyFile, user) => KeyFilePath = keyFilePath;
	}
	
	public string User { get; }

	SFtpConfig(Ids id, string user)
	{
		Id = id;
		User = user;
	}
	
	// equality and internal members ....
}

// match helper ....
```

With that the consumer can now be implemented in a 'type safe' manner:

```csharp
public class Consumer
{
	public FtpConnection Connect(SFtpConfig config)
	{
		return config.Match(
			withPassword => ConnectWithPassword(withPassword.User, withPassword.Password),
			withKeyFile => ConnectWithKeyFile(withKeyFile.User, withKeyFile.KeyFilePath)
			);
	}

	FtpConnection ConnectWithKeyFile(string argUser, string argKeyFilePath) => throw new NotImplementedException();

	FtpConnection ConnectWithPassword(string withPasswordUser, string withPasswordPassword) => throw new NotImplementedException();
}
```







 
