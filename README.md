# RemoteShutdown

Remotely shut Windows down if your Welcome Screen crashes by USB tethering your iPhone with the computer and accessing the RemoteShutdown web app via IP.

## Motivation

In earlier versions of Windows (e.g., 8, 8.1 and some versions of 10), when the Welcome Screen crashes (you see blank screen with pointer after flicking up the Lock Screen), you can use RD client to connect to it, which makes the Welcome Screen work again.

However, this workaround seems to stop working in recent versions of Windows 10. When I RDP into the workstation, the RD session is stuck at Welcome Screen, and any UI interaction with it will cause it to be frozen, so you cannot restart your computer (cleanly) by going into the Welcome Screen and choosing Restart from the power options. Moreover, once RD session is established, the workstation console will be stuck at the Lock Screen (you cannot flick it up). However, you can hold the Power button to launch "Slide down to shut down your PC" app. Unfortunately, sliding down won't shut down your PC. My guess is that it tries to shut Windows down without force, so the Welcome Screen is supposed to notify you that some users are still logged in and ask whether you want to forcibly shut Windows down, except it can't because it has crashed. Voila, a deadlock.

It is found that IIS still runs OK even if the Welcome Screen stops responding. I don't know whether this web app will succeed in case Welcome Screen indeed crashes --- I haven't been "lucky" enough to see this phenomenon again. I suppose it should.

## Usage

By default, the project deploys to `C:\inetpub\wwwroot`. You should bind this website to `localhost` and any unassigned IP (or IP addresses in the range `172.20.10.*`). Launch the website, enter the credential (the domain should be left empty if you don't know what it is), choose the action (restart or shutdown) and tap "Invoke".

**WARNING** If the credential is correct and the user holds sufficient privilege, the system will forcibly shut down or restart. You will have no oppotunity to cancel and return to your desktop to save your works. Be sure to use auto-save programs or save all your works before trying.

## Security considerations

The security of this app is not audited. Use at your own risk. Current security measures:

- The host name must be `localhost`, `127.0.0.1` or `172.20.10.*` (iPhone tethering IP address range).
- Strict parameter validation:
    - The only allowed parameters are `domain`, `user`, `password` and `action`.
    - All these parameters are required and be less than 121 UTF-16 units.
    - No parameter can appear twice.
    - `user` must be non-empty.
    - `action` must be `shutdown` or `restart`.
- Disallows empty password logon, i.e., even if there's a user with shutdown privilege whose password is empty, you cannot use it to remotely shut Windows down.
- Controls the frequency of password attempts.
- Exception details are suppressed if it's not loop-back.

It is advisable but almost impossible to use an HTTPS certificate.

## License

The MIT license.
