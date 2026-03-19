#!zsh

set -e
cd "$(git rev-parse --show-toplevel)"

print_help() {
	cat <<help
usage: ./t [options] [command]

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

mods=()
for dir in ./*/manifest.json(N); do
	mod="${dir:h:t}"
	[[ "$mod" = "build" ]] && continue
	mods+=("$mod")
done

command="$1"
shift
case "$command" in $"\0")
	;;"build")
		zparseopts -D -M -- {r,-restart}=restart
		for mod in "${mods[@]}"; do
			pushd "./$mod"
			RESTART="$([[ -n $restart ]] && echo true || echo false)" dotnet build -v:d -p:Mod="$mod" || exit 1
			popd
		done
	;;"clean")
  	git clean -fdX
	;;"watch")
		zparseopts -D -M -- {r,-restart}=restart
		function free { echo "killing"; kill -9 $(pgrep -P $$) || true; }
		trap free INT TERM
		for mod in "${mods[@]}"; do
			pushd "./$mod"
			RESTART="$([[ -n $restart ]] && echo true || echo false)" dotnet watch build -- -p:Mod="$mod" &
			popd
		done
		wait
	;;"pack")
		pushd "./build"
		for mod in "${mods[@]}"; do
			pushd "./$mod"
			zip "../$mod.zip" ./*
			popd
		done
		popd
	;;"start")
		echo "starting"
		/Applications/Steam.app/Contents/MacOS/steam_osx -applaunch 1062090 -skipModManager
	;;"kill")
		echo "killing"
		killall Timberborn
	;;"restart")
		echo "restarting"
		./t kill && ./t start || true
	;;"link")
		for mod in "${mods[@]}"; do
			here="$(pwd)"
			pushd ~/Documents/Timberborn/Mods
			ln -s "$here/build/$mod" . || true
			popd
		done
	;;*)
		print_help && exit 1
esac
