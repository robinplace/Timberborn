#!zsh

set -e
cd "$(git rev-parse --show-toplevel)"

print_help() {
	cat <<help
usage: ./toolool [options] [command]

options:
	-h, --help  print this help message

commands:
	build	   build all packages
	watch	   watch build all packages
	pack	   zip all packages
	start	   start timberborn
	restart	 restart timberborn, if it is running
	kill	   kill timberborn
help
}
zparseopts -D -M -- {h,-help}=help
if [[ -v help[1] ]]; then print_help; exit; fi

project=()
for dir in ./*/*.csproj(N); do
	project="${dir:h:t}"
	projects+=("$project")
done

mods=()
for dir in ./*/manifest.json(N); do
	mod="${dir:h:t}"
	mods+=("$mod")
done

command="$1"
shift
case "$command" in $"\0")
	;;"build")
		zparseopts -D -M -- {r,-restart}=restart
		for mod in "${mods[@]}"; do
			pushd "./$mod"
			dotnet build -v:d -p:MOD="$mod" &
			popd
		done
		wait
		[[ -n $restart ]] && ./tool restart
	;;"clean")
  	git clean -fdX
	;;"watch")
		zparseopts -D -M -- {r,-restart}=restart
		function free {
			CH="$(pgrep -P $$ | tr '\n' ' ')"
			if [[ "$CH" != "" ]]; then echo "killing $CH"; kill $(pgrep -P $$)
			else echo "killed"; fi
		}
		trap free INT TERM EXIT
		for mod in "${mods[@]}"; do
			pushd "./$mod"
			RESTART="$([[ -n $restart ]] && echo true || echo false)" dotnet watch build --non-interactive -- -p:MOD="$mod" &
			popd
		done
		wait
		free
	;;"pack")
		for mod in "${mods[@]}"; do
			pushd "./$mod/mod"
			zip "../../$mod.zip" ./$mod/*
			popd
		done
		popd
	;;"start")
		zparseopts -D -M -- {m,-menu}=menu
		SKIP="$([[ -n $menu ]] && echo "" || echo "-skipModManager")"
		echo "starting"
		/Applications/Steam.app/Contents/MacOS/steam_osx -applaunch 1062090 $SKIP
	;;"kill")
		echo "killing"
		killall Timberborn
	;;"restart")
		echo "restarting"
		./tool kill && ./tool start || true
	;;"link")
		for mod in "${mods[@]}"; do
			here="$(pwd)"
			pushd ~/Documents/Timberborn/Mods
			rm "$mod" || true
			ln -s "$here/$mod/mod/$mod" "$mod" || true
			popd
		done
	;;"format")
		for project in "${projects[@]}"; do
			dotnet format "$project/$project.csproj"
		done
	;;*)
		print_help && exit 1
esac
