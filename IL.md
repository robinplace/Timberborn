
		var module = ModuleDefinition.ReadModule(typeof(Ticker).Assembly.Location);
		var entry = module.GetType(typeof(Ticker).FullName).FindMethod(nameof(Ticker.TickOnce));
		Recurse(entry!);
	}

	readonly HashSet<MethodDefinition> method_set = [];

	private void Recurse(MethodDefinition entry) {
		if (method_set.Add(entry)) {
			Debug.Log($"recurse method {entry.DeclaringType.FullName}::{entry.Name}");
		} else {
			Debug.Log($"repeat method {entry.DeclaringType.FullName}::{entry.Name}");
			return;
		}
		var il = new ILContext(entry);
		var cur = new ILCursor(il).Goto(0);
		Debug.Log($"scan method {entry.DeclaringType.FullName}::{entry.Name}");
		while (true) {
			FieldReference? field_ref = null;
			MethodReference? method_ref = null;
			if (!cur.TryGotoNext(i => (
				i.MatchStfld(out field_ref) ||
				i.MatchCallvirt(out method_ref)
			)))
				break;
			if (field_ref != null) {
				var field = field_ref.Resolve();
				Debug.Log($"set field {field.DeclaringType.FullName}::{field.Name}");
			}
			if (method_ref != null) {
				var method = method_ref.Resolve();
				Debug.Log($"call virt {method.DeclaringType.FullName}::{method.Name}");
				Debug.Log($"body {method.HasBody}");
				Recurse(method);
			}
		}
	}

	/*
			if (method_set.Add(method_ref)) {
				Debug.Log($"hook method {method.DeclaringType.FullName}::{method.Name}");
				hook_set.Add(new ILHook(
					method,
					RecursiveManipulate
				));
			} else {
				Debug.Log($"repeat method {method.DeclaringType.FullName}::{method.Name}");
			}
		readonly HashSet<ILHook> hook_set = [];

		private void RecursiveManipulate(ILContext il) {
			var cur = new ILCursor(il).Goto(0);
			while (true) {
				FieldReference? field_ref = null;
				MethodReference? method_ref = null;
				if (!cur.TryGotoNext(i => (
					i.MatchStfld(out field_ref) ||
					i.MatchCallvirt(out method_ref)
				))) break;
				if (field_ref != null) {
					var field = field_ref.ResolveReflection();
					Debug.Log($"set field {field.DeclaringType.FullName}::{field.Name}");
				}
				if (method_ref != null) {
					var method = method_ref.ResolveReflection();
					Debug.Log($"call virt {method.DeclaringType.FullName}::{method.Name}");
					RecurseHook(method);
				}
			}
		}*/
