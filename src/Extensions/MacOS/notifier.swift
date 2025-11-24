import Foundation
import UserNotifications

// Command line arguments:
// 1. JSON payload string
//
// JSON Payload Structure:
// {
//   "id": "guid",
//   "title": "string",
//   "message": "string",
//   "group": "string?",
//   "actions": [
//     { "id": "string", "label": "string" }
//   ],
//   "pipePath": "string?"  // Optional - for IPC via named pipe
// }

struct NotificationAction: Codable {
    let id: String
    let label: String
}

struct NotificationPayload: Codable {
    let id: String
    let title: String
    let message: String
    let group: String?
    let actions: [NotificationAction]?
    let pipePath: String?  // Optional - for IPC via named pipe
}

class NotificationDelegate: NSObject, UNUserNotificationCenterDelegate {
    func userNotificationCenter(_ center: UNUserNotificationCenter, willPresent notification: UNNotification, withCompletionHandler completionHandler: @escaping (UNNotificationPresentationOptions) -> Void) {
        // Show notification even if app is in foreground (though for a CLI tool this matters less)
        completionHandler([.banner, .sound, .list])
    }

    func userNotificationCenter(_ center: UNUserNotificationCenter, didReceive response: UNNotificationResponse, withCompletionHandler completionHandler: @escaping () -> Void) {
        let userInfo = response.notification.request.content.userInfo

        if let notificationId = userInfo["notificationId"] as? String {
            let actionId = response.actionIdentifier

            // Output JSON to stdout for the parent process to read (for logging/debugging)
            let output = [
                "type": "action",
                "notificationId": notificationId,
                "actionId": actionId
            ]

            if let jsonData = try? JSONSerialization.data(withJSONObject: output, options: []),
               let jsonString = String(data: jsonData, encoding: .utf8) {
                print(jsonString)
                fflush(stdout) // Ensure output is flushed immediately
            }

            // Write to named pipe if pipePath is provided (IPC mechanism)
            if let pipePath = userInfo["pipePath"] as? String, !pipePath.isEmpty {
                let signal = "\(actionId)\n"
                if let signalData = signal.data(using: .utf8) {
                    do {
                        if let fileHandle = FileHandle(forWritingAtPath: pipePath) {
                            fileHandle.write(signalData)
                            try fileHandle.close()
                        } else {
                            fputs("Warning: Could not open pipe at \(pipePath)\n", stderr)
                        }
                    } catch {
                        fputs("Warning: Failed to write to pipe: \(error.localizedDescription)\n", stderr)
                    }
                }
            }
        }

        completionHandler()
        exit(0) // Exit after handling the action
    }
}

// Main execution
let center = UNUserNotificationCenter.current()
let delegate = NotificationDelegate()
center.delegate = delegate

// Request permission
center.requestAuthorization(options: [.alert, .sound, .badge]) { granted, error in
    if let error = error {
        fputs("Auth Error: \(error.localizedDescription)\n", stderr)
    }
    if !granted {
        fputs("Permission denied (granted=false)\n", stderr)
        exit(1)
    }
}

// Parse arguments
guard CommandLine.arguments.count > 1 else {
    fputs("Usage: notifier <json_payload>\n", stderr)
    exit(1)
}

let jsonString = CommandLine.arguments[1]
guard let jsonData = jsonString.data(using: .utf8),
      let payload = try? JSONDecoder().decode(NotificationPayload.self, from: jsonData) else {
    fputs("Invalid JSON payload\n", stderr)
    exit(1)
}

// Create content
let content = UNMutableNotificationContent()
content.title = payload.title
content.body = payload.message
content.sound = .default
content.userInfo = [
    "notificationId": payload.id,
    "pipePath": payload.pipePath ?? ""
]

if let group = payload.group {
    content.threadIdentifier = group
}

// Add actions
if let actions = payload.actions, !actions.isEmpty {
    let categoryId = "category-\(payload.id)"
    content.categoryIdentifier = categoryId
    
    let nativeActions = actions.map { action in
        UNNotificationAction(identifier: action.id, title: action.label, options: [])
    }
    
    let category = UNNotificationCategory(identifier: categoryId, actions: nativeActions, intentIdentifiers: [], options: [])
    center.setNotificationCategories([category])
}

// Create request
let request = UNNotificationRequest(identifier: payload.id, content: content, trigger: nil)

// Send
center.add(request) { error in
    if let error = error {
        fputs("Error: \(error.localizedDescription)\n", stderr)
        exit(1)
    }
}

// Keep running to wait for delegate callbacks
// For a CLI tool, we need a run loop to keep the process alive long enough to receive the "delivered" event or action click
// In this simple sidecar, we might just exit if we don't care about waiting for actions in the *same* process run,
// BUT for actions to work, the user clicks the notification later.
//
// Strategy:
// The sidecar sends the notification and then exits.
// WAIT: If the sidecar exits, the delegate won't be around to handle the click?
// On macOS, if the app (sidecar) is not running, clicking the notification *launches* it.
// However, since this is a command line tool, launching it again might be tricky without arguments.
//
// Alternative Strategy for CLI tools:
// Use "alert" style notifications which stay until dismissed.
// But we want to handle actions.
//
// If the user clicks a button, macOS tries to launch the app bundle.
// Since we are a standalone binary, we might not be a proper "app bundle".
//
// Let's try keeping the process alive for a short duration or until interaction?
// No, that blocks the main app if we wait.
//
// Actually, for a CLI tool, handling async actions from system notifications is hard without being a bundled .app.
//
// Let's assume for now we just want to SEND the notification.
// If we want to handle actions, we might need to be an .app bundle or use a different mechanism.
//
// However, `osascript` couldn't do buttons.
// This swift tool CAN do buttons, but handling the callback is the issue.
//
// If we run `RunLoop.main.run()`, it blocks.
//
// Let's try running the loop. The parent process (C#) can launch this asynchronously and listen to stdout.
// If the user clicks "Close" or ignores it, this process might hang forever?
// We can add a timeout.

// Run loop with timeout (e.g., 30 seconds? Or just let it run?)
// If we want to support actions, we must stay alive.
RunLoop.main.run()
