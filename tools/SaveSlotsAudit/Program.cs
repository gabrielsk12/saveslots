using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Resources;
using System.Security.Cryptography;
using System.Text;

internal static class Program
{
	private const string AuthorSuppliedLogoSha256 = "E940EB397C88D3E366288C66B57B2D0E5EDEE3C2D6051B59A267D885209D20D0";
	private const string AuthorSuppliedFallbackSha256 = "B473BF07C78242FF22F8320D44D16CBA91693F6FD77F38A411C73E91F75F0462";
	private const string CreditedTransitionSoundSha256 = "0A63859FE51750083889EA84DFA05525CFFAD20D4977C7EE178552FE630AAE86";
	private const string CreditedUiClickSoundSha256 = "49B6FC5C7F1ED45029D55D03FFCA131DB9F378D0365A51B9A5657D59AA89440C";
	private static readonly string[] ForbiddenTypes =
	{
		"SlotsManager", "SlotBehaviour", "CustomExtensions", "ButtonSaves", "DeleteSaveButton",
		"LoadingBehaviour", "ModPrompt", "ModSave", "ResizeOnHover"
	};

	private static int Main(string[] args)
	{
		if (args.Length < 4)
		{
			Console.Error.WriteLine("Usage: SaveSlotsAudit <new SaveSlots.dll> <immutable old MWC SaveSlots.dll> <package directory> <MSC 1.1 reference DLL>");
			return 2;
		}
		string current = Path.GetFullPath(args[0]);
		string reference = Path.GetFullPath(args[1]);
		string package = Path.GetFullPath(args[2]);
		string mscReference = Path.GetFullPath(args[3]);
		Require(File.Exists(current), "new DLL exists");
		Require(File.Exists(reference), "reference DLL exists");
		Require(Directory.Exists(package), "package directory exists");
		Require(File.Exists(mscReference), "MSC 1.1 audit reference exists");

		using FileStream stream = File.OpenRead(current);
		using PEReader pe = new PEReader(stream);
		Require(pe.HasMetadata, "new DLL has CLR metadata");
		MetadataReader metadata = pe.GetMetadataReader();
		AssemblyDefinition assembly = metadata.GetAssemblyDefinition();
		Require(metadata.GetString(assembly.Name) == "SaveSlots", "assembly name is SaveSlots");
		Require(assembly.Version == new Version(4, 0, 0, 0), "assembly version is 4.0.0.0");
		Require(metadata.ManifestResources.Count == 4, "assembly contains only the two approved images and two credited sounds");
		HashSet<string> resourceNames = new HashSet<string>(metadata.ManifestResources.Select(handle => metadata.GetString(metadata.GetManifestResource(handle).Name)), StringComparer.Ordinal);
		Require(resourceNames.SetEquals(new[]
		{
			"MwcSaveSlots.logo.png",
			"MwcSaveSlots.fallback-thumbnail.png",
			"MwcSaveSlots.transition-camera.wav",
			"MwcSaveSlots.ui-button-click.wav"
		}), "embedded resources match the approved image and sound allowlist");

		HashSet<string> types = new HashSet<string>(StringComparer.Ordinal);
		foreach (TypeDefinitionHandle handle in metadata.TypeDefinitions)
		{
			TypeDefinition type = metadata.GetTypeDefinition(handle);
			string name = metadata.GetString(type.Name);
			string ns = metadata.GetString(type.Namespace);
			types.Add(ns + "." + name);
			Require(!ForbiddenTypes.Contains(name, StringComparer.Ordinal), "forbidden former type is absent: " + name);
			Require(ns != "SaveSlots", "former SaveSlots namespace is absent");
		}
		string[] requiredTypes =
		{
			"MwcSaveSlots.MwcSaveSlotsMod", "MwcSaveSlots.ProfileCoordinator", "MwcSaveSlots.ProfileRepository",
			"MwcSaveSlots.SnapshotTransaction", "MwcSaveSlots.SaveMetadataReader", "MwcSaveSlots.ThumbnailService",
			"MwcSaveSlots.SaveSlotsMenuView", "MwcSaveSlots.GameMenuBridge", "MwcSaveSlots.DiagnosticWriter",
			"MwcSaveSlots.MwcAssetCatalog", "MwcSaveSlots.UiPanelAnimator", "MwcSaveSlots.MenuButtonMotion",
			"MwcSaveSlots.UiSoundPlayer", "MwcSaveSlots.PlayerNameFormatter"
		};
		foreach (string type in requiredTypes) Require(types.Contains(type), "new architecture type exists: " + type);

		HashSet<string> references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, AssemblyReferenceHandle> referenceHandles = new Dictionary<string, AssemblyReferenceHandle>(StringComparer.OrdinalIgnoreCase);
		foreach (AssemblyReferenceHandle handle in metadata.AssemblyReferences)
		{
			string name = metadata.GetString(metadata.GetAssemblyReference(handle).Name);
			references.Add(name);
			referenceHandles[name] = handle;
		}
		foreach (string expected in new[] { "MSCLoader", "UnityEngine", "UnityEngine.UI", "ES2" })
		{
			Require(references.Contains(expected), "expected dependency exists: " + expected);
		}
		foreach (string framework in new[] { "mscorlib", "System", "System.Core", "System.Xml" })
		{
			Require(referenceHandles.ContainsKey(framework), "MWC Mono framework reference exists: " + framework);
			AssemblyReference runtimeReference = metadata.GetAssemblyReference(referenceHandles[framework]);
			Require(runtimeReference.Version == new Version(2, 0, 5, 0), framework + " targets MWC Mono version 2.0.5.0");
			string token = Convert.ToHexString(metadata.GetBlobBytes(runtimeReference.PublicKeyOrToken));
			Require(token.Equals("7CEC85D7BEA7798E", StringComparison.OrdinalIgnoreCase), framework + " targets the MWC Mono public key token");
		}

		byte[] image = File.ReadAllBytes(current);
		Require(Contains(image, "SaveSlotsMWC"), "mod ID string is present");
		Require(Contains(image, "SAVE SLOTS"), "display name string is present");
		Require(Contains(image, "Gabriel_SK"), "author string is present");
		Require(!Contains(image, "InvalidDataException"), "unsupported Unity Mono InvalidDataException reference is absent");
		Require(!Contains(image, "SaveSlots.Properties.Resources"), "former resource wrapper is absent");
		Require(!Contains(image, "SaveSlotsCanvas.prefab"), "MSC-derived canvas prefab reference is absent");
		Require(!Contains(image, "SlotsManager"), "MSC SlotsManager symbol is absent from the release binary");
		Require(!Contains(image, "SlotBehaviour"), "MSC SlotBehaviour symbol is absent from the release binary");
		Require(!Contains(image, "ResizeOnHover"), "MSC ResizeOnHover symbol is absent from the release binary");
		Require(!Contains(image, "ButtonSaves"), "MSC ButtonSaves symbol is absent from the release binary");
		VerifyApprovedResources(current, reference, mscReference);
		Require(!string.Equals(Hash(current), Hash(reference), StringComparison.OrdinalIgnoreCase), "new and reference hashes differ");
		Require(!string.Equals(Hash(current), Hash(mscReference), StringComparison.OrdinalIgnoreCase), "new and MSC reference hashes differ");

		HashSet<string> allowed = new HashSet<string>(new[]
		{
			"SaveSlots.dll", "README.txt", "NEXUS_DESCRIPTION.txt", "ORIGINALITY_REPORT.txt", "CHANGELOG.txt"
		}, StringComparer.OrdinalIgnoreCase);
		string[] files = Directory.GetFiles(package, "*", SearchOption.AllDirectories);
		Require(files.Length == allowed.Count, "package has exactly five approved files");
		foreach (string file in files) Require(allowed.Contains(Path.GetFileName(file)), "package file is approved: " + Path.GetFileName(file));

		Console.WriteLine("PASS binary identity, MWC Mono compatibility, independent runtime UI, MSC asset exclusion, dependency, type, approved-resource provenance, hash, and package audit");
		Console.WriteLine("new sha256=" + Hash(current));
		Console.WriteLine("MWC v3 reference sha256=" + Hash(reference));
		Console.WriteLine("MSC 1.1 audit reference sha256=" + Hash(mscReference));
		return 0;
	}

	private static bool Contains(byte[] bytes, string text)
	{
		return Find(bytes, Encoding.UTF8.GetBytes(text)) || Find(bytes, Encoding.Unicode.GetBytes(text));
	}

	private static void VerifyApprovedResources(string current, string reference, string mscReference)
	{
		Assembly currentAssembly = Assembly.LoadFile(current);
		Assembly referenceAssembly = Assembly.LoadFile(reference);
		Assembly mscAssembly = Assembly.LoadFile(mscReference);
		byte[] currentLogo = ReadRawResource(currentAssembly, "MwcSaveSlots.logo.png");
		byte[] currentFallback = ReadRawResource(currentAssembly, "MwcSaveSlots.fallback-thumbnail.png");
		byte[] currentTransitionSound = ReadRawResource(currentAssembly, "MwcSaveSlots.transition-camera.wav");
		byte[] currentUiClickSound = ReadRawResource(currentAssembly, "MwcSaveSlots.ui-button-click.wav");
		Dictionary<string, byte[]> referenceAssets = ReadAssets(referenceAssembly, "SaveSlots.Properties.Resources.resources");
		Dictionary<string, byte[]> mscAssets = ReadAssets(mscAssembly, "SaveSlots.Properties.Resources.resources");
		Require(referenceAssets.ContainsKey("logo"), "MWC v3 reference contains its former logo");
		Require(mscAssets.ContainsKey("logo"), "MSC audit reference contains its logo");
		Require(mscAssets.ContainsKey("saveslots"), "MSC audit reference contains its UI bundle");
		Require(Hash(currentLogo).Equals(AuthorSuppliedLogoSha256, StringComparison.OrdinalIgnoreCase), "embedded logo matches the new author-supplied PNG");
		Require(Hash(currentFallback).Equals(AuthorSuppliedFallbackSha256, StringComparison.OrdinalIgnoreCase), "embedded fallback matches the author-supplied monochrome PNG");
		Require(Hash(currentTransitionSound).Equals(CreditedTransitionSoundSha256, StringComparison.OrdinalIgnoreCase), "embedded transition sound matches the documented Unity-compatible conversion");
		Require(Hash(currentUiClickSound).Equals(CreditedUiClickSoundSha256, StringComparison.OrdinalIgnoreCase), "embedded UI click matches the documented Unity-compatible conversion");
		Require(!currentUiClickSound.SequenceEqual(currentTransitionSound), "UI click and transition sounds are distinct");
		Require(!referenceAssets.Values.Any(value => currentUiClickSound.SequenceEqual(value)), "UI click is not copied from the MWC v3 resource set");
		Require(!mscAssets.Values.Any(value => currentUiClickSound.SequenceEqual(value)), "UI click is not copied from the MSC resource set");
		Require(!currentLogo.SequenceEqual(referenceAssets["logo"]), "new logo is not copied from the MWC v3 reference");
		Require(!currentLogo.SequenceEqual(mscAssets["logo"]), "embedded MWC logo is not the MSC logo");
		Require(!currentLogo.SequenceEqual(mscAssets["saveslots"]), "embedded MWC logo is not the MSC UI bundle");
		Require(!currentFallback.SequenceEqual(referenceAssets["logo"]), "fallback thumbnail is not the former MWC logo");
		Require(!currentFallback.SequenceEqual(mscAssets["logo"]), "fallback thumbnail is not the MSC logo");
		Require(!currentFallback.SequenceEqual(mscAssets["saveslots"]), "fallback thumbnail is not the MSC UI bundle");
	}

	private static byte[] ReadRawResource(Assembly assembly, string resourceName)
	{
		using Stream? stream = assembly.GetManifestResourceStream(resourceName);
		Require(stream != null, "manifest resource is readable: " + resourceName);
		using MemoryStream memory = new MemoryStream();
		stream!.CopyTo(memory);
		return memory.ToArray();
	}

	private static Dictionary<string, byte[]> ReadAssets(Assembly assembly, string resourceName)
	{
		Dictionary<string, byte[]> result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
		using Stream? stream = assembly.GetManifestResourceStream(resourceName);
		Require(stream != null, "manifest resource is readable: " + resourceName);
		using ResourceReader reader = new ResourceReader(stream!);
		System.Collections.IDictionaryEnumerator entries = reader.GetEnumerator();
		while (entries.MoveNext())
		{
			if (entries.Key is string key && entries.Value is byte[] bytes) result[key] = bytes;
		}
		return result;
	}

	private static bool Find(byte[] source, byte[] value)
	{
		for (int i = 0; i <= source.Length - value.Length; i++)
		{
			int j = 0;
			for (; j < value.Length && source[i + j] == value[j]; j++) { }
			if (j == value.Length) return true;
		}
		return false;
	}

	private static string Hash(string path)
	{
		using FileStream stream = File.OpenRead(path);
		using SHA256 sha = SHA256.Create();
		return Convert.ToHexString(sha.ComputeHash(stream));
	}

	private static string Hash(byte[] bytes)
	{
		using SHA256 sha = SHA256.Create();
		return Convert.ToHexString(sha.ComputeHash(bytes));
	}

	private static void Require(bool condition, string statement)
	{
		if (!condition) throw new InvalidOperationException("Audit failed: " + statement);
	}
}
