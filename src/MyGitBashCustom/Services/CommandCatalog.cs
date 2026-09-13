using MyGitBashCustom.Models;

namespace MyGitBashCustom.Services;

/// <summary>کاتالوگ کامل دستورات git و bash با توضیح فارسی/انگلیسی و مثال.</summary>
public static class CommandCatalog
{
    public static readonly IReadOnlyList<GitCommandInfo> All = new List<GitCommandInfo>
    {
        // ── GIT ──
        new("git init", "git init [name]", "ساخت مخزن گیت جدید", "Create a new repository", CommandCategory.Git, "git init my-project", ["init"]),
        new("git clone", "git clone <url>", "کپی یک مخزن راه‌دور", "Clone a remote repository", CommandCategory.Git, "git clone https://github.com/user/repo.git", ["clone"]),
        new("git status", "git status [-s]", "نمایش وضعیت فایل‌ها", "Show working tree status", CommandCategory.Git, "git status -s", ["st", "status"]),
        new("git add", "git add <file|.>", "افزودن فایل به stage", "Add files to staging", CommandCategory.Git, "git add .", ["add"]),
        new("git commit", "git commit -m \"msg\"", "ثبت تغییرات", "Record changes", CommandCategory.Git, "git commit -m \"feat: login\"", ["ci", "commit"]),
        new("git push", "git push [remote] [branch]", "ارسال به راه‌دور", "Push to remote", CommandCategory.Git, "git push origin main", ["push"]),
        new("git pull", "git pull [remote] [branch]", "دریافت + ادغام", "Fetch and merge", CommandCategory.Git, "git pull origin main", ["pull"]),
        new("git fetch", "git fetch [remote]", "دریافت بدون ادغام", "Fetch without merging", CommandCategory.Git, "git fetch --all --prune", ["fetch"]),
        new("git branch", "git branch [name]", "مدیریت شاخه‌ها", "Manage branches", CommandCategory.Git, "git branch feature/x", ["br", "branch"]),
        new("git checkout", "git checkout <branch>", "جابه‌جایی شاخه/بازیابی", "Switch branches", CommandCategory.Git, "git checkout -b feature/x", ["co", "checkout"]),
        new("git switch", "git switch <branch>", "جابه‌جایی مدرن شاخه", "Modern branch switch", CommandCategory.Git, "git switch -c feature/x", ["switch"]),
        new("git merge", "git merge <branch>", "ادغام شاخه", "Merge branches", CommandCategory.Git, "git merge feature/x", ["merge"]),
        new("git rebase", "git rebase <base>", "بازنشانی تاریخچه روی پایه", "Rebase history", CommandCategory.Git, "git rebase main", ["rebase"]),
        new("git log", "git log [--oneline --graph]", "نمایش تاریخچه", "Show history", CommandCategory.Git, "git log --oneline --graph -15", ["lg", "log"]),
        new("git diff", "git diff [file]", "نمایش تفاوت‌ها", "Show differences", CommandCategory.Git, "git diff --staged", ["diff"]),
        new("git stash", "git stash [push/pop]", "کنارگذاشتن موقت تغییرات", "Stash changes", CommandCategory.Git, "git stash push -m wip", ["stash"]),
        new("git reset", "git reset [--soft|--hard] [commit]", "بازنشانی HEAD", "Reset HEAD", CommandCategory.Git, "git reset --soft HEAD~1", ["reset"]),
        new("git revert", "git revert <commit>", "برگرداندن امن یک کامیت", "Safe revert", CommandCategory.Git, "git revert HEAD", ["revert"]),
        new("git cherry-pick", "git cherry-pick <sha>", "برداشتن یک کامیت خاص", "Pick a commit", CommandCategory.Git, "git cherry-pick abc1234", ["cherry-pick"]),
        new("git remote", "git remote -v", "مدیریت ریموت‌ها", "Manage remotes", CommandCategory.Git, "git remote add origin <url>", ["remote"]),
        new("git tag", "git tag <v1.0.0>", "مدیریت تگ نسخه", "Manage tags", CommandCategory.Git, "git tag -a v1.0.0 -m release", ["tag"]),
        new("git show", "git show [commit]", "نمایش جزئیات کامیت", "Show commit details", CommandCategory.Git, "git show HEAD --stat", ["show"]),
        new("git blame", "git blame <file>", "مشخص‌کردن نویسنده هر خط", "Blame lines", CommandCategory.Git, "git blame src/App.cs", ["blame"]),
        new("git clean", "git clean -fd", "حذف فایل‌های trackنشده", "Remove untracked files", CommandCategory.Git, "git clean -n", ["clean"]),
        new("git restore", "git restore <file>", "بازیابی فایل", "Restore file", CommandCategory.Git, "git restore --staged .", ["restore"]),
        new("git config", "git config --global user.name \"x\"", "تنظیمات گیت", "Git config", CommandCategory.Git, "git config --global user.email a@b.c", ["config"]),
        // ── BASH / FILE ──
        new("ls", "ls [-la]", "فهرست فایل‌ها", "List files", CommandCategory.Bash, "ls -la", ["ll", "ls"]),
        new("cd", "cd <dir>", "تغییر پوشه", "Change directory", CommandCategory.Bash, "cd ~/projects", ["cd"]),
        new("pwd", "pwd", "نمایش مسیر جاری", "Print working dir", CommandCategory.Bash, "pwd", ["pwd"]),
        new("mkdir", "mkdir [-p] <dir>", "ساخت پوشه", "Make directory", CommandCategory.Bash, "mkdir -p src/app", ["mkdir"]),
        new("touch", "touch <file>", "ساخت فایل خالی", "Create empty file", CommandCategory.Bash, "touch notes.txt", ["touch"]),
        new("cp", "cp [-r] <src> <dst>", "کپی", "Copy", CommandCategory.File, "cp -r a b", ["cp"]),
        new("mv", "mv <src> <dst>", "انتقال/تغییرنام", "Move/rename", CommandCategory.File, "mv old.txt new.txt", ["mv", "rename"]),
        new("rm", "rm [-rf] <path>", "حذف (با احتیاط!)", "Remove (careful!)", CommandCategory.File, "rm -rf ./dist", ["rm", "del"]),
        new("cat", "cat <file>", "نمایش محتوای فایل", "Show file", CommandCategory.File, "cat README.md", ["cat", "type"]),
        new("less", "less <file>", "نمایش صفحه‌به‌صفحه", "Pager view", CommandCategory.File, "less big.log", ["less", "more"]),
        new("head", "head [-n 20] <file>", "چند خط اول", "First lines", CommandCategory.File, "head -n 20 log.txt", ["head"]),
        new("tail", "tail [-f] <file>", "چند خط آخر / دنبال‌کردن", "Last lines / follow", CommandCategory.File, "tail -f app.log", ["tail"]),
        new("grep", "grep [-rni] <pattern>", "جستجو در متن", "Search text", CommandCategory.Bash, "grep -rni \"TODO\" src", ["grep", "findstr"]),
        new("find", "find <dir> -name \"*.cs\"", "جستجوی فایل", "Find files", CommandCategory.Bash, "find . -name \"*.xaml\"", ["find"]),
        new("echo", "echo <text>", "چاپ متن", "Print text", CommandCategory.Bash, "echo hello", ["echo"]),
        new("export", "export VAR=value", "تعریف متغیر محیطی", "Set env var", CommandCategory.Bash, "export NODE_ENV=prod", ["export", "set"]),
        new("alias", "alias ll='ls -la'", "نام مستعار دستور", "Command alias", CommandCategory.Bash, "alias gs='git status'", ["alias"]),
        new("history", "history [n]", "تاریخچه دستورات", "Command history", CommandCategory.Bash, "history 20", ["history"]),
        new("clear", "clear", "پاک‌کردن صفحه", "Clear screen", CommandCategory.Bash, "clear", ["clear", "cls"]),
        new("chmod", "chmod +x <file>", "تغییر دسترسی", "Change permissions", CommandCategory.System, "chmod +x run.sh", ["chmod"]),
        new("chown", "chown user:group <f>", "تغییر مالک", "Change owner", CommandCategory.System, "chown $USER file", ["chown"]),
        new("ps", "ps aux", "فرایندها", "Processes", CommandCategory.System, "ps aux | grep dotnet", ["ps"]),
        new("kill", "kill <pid>", "پایان فرایند", "Kill process", CommandCategory.System, "kill -9 1234", ["kill"]),
        new("df", "df -h", "فضای دیسک", "Disk space", CommandCategory.System, "df -h", ["df"]),
        new("du", "du -sh *", "حجم پوشه‌ها", "Dir sizes", CommandCategory.System, "du -sh * | sort -h", ["du"]),
        new("tar", "tar -czf a.tar.gz <dir>", "بسته‌بندی", "Archive", CommandCategory.File, "tar -xzf a.tar.gz", ["tar"]),
        new("zip", "zip -r a.zip <dir>", "فشرده‌سازی zip", "Zip compress", CommandCategory.File, "unzip a.zip", ["zip", "unzip"]),
        new("curl", "curl [-I] <url>", "درخواست HTTP", "HTTP request", CommandCategory.Network, "curl -I https://api.github.com", ["curl"]),
        new("wget", "wget <url>", "دانلود فایل", "Download file", CommandCategory.Network, "wget https://.../f.zip", ["wget"]),
        new("ssh", "ssh user@host", "اتصال امن", "Secure shell", CommandCategory.Network, "ssh -i key.pem user@host", ["ssh"]),
        new("scp", "scp <src> user@h:<dst>", "کپی امن راه‌دور", "Secure copy", CommandCategory.Network, "scp app.zip user@s:/tmp/", ["scp"]),
        new("ping", "ping <host>", "تست اتصال", "Ping host", CommandCategory.Network, "ping -c 4 8.8.8.8", ["ping"]),
        new("env", "env | grep X", "متغیرهای محیطی", "Environment vars", CommandCategory.System, "env | sort", ["env", "printenv"]),
        new("which", "which <cmd>", "مسیر دستور", "Locate command", CommandCategory.System, "which git", ["which", "where"]),
        new("nano", "nano <file>", "ویرایشگر ساده", "Simple editor", CommandCategory.File, "nano README.md", ["nano"]),
        new("vim", "vim <file>", "ویرایشگر vim", "Vim editor", CommandCategory.File, "vim main.c", ["vi", "vim"]),
        new("code", "code <dir>", "بازکردن در VS Code", "Open in VS Code", CommandCategory.Custom, "code .", ["code"]),
        new("dotnet", "dotnet [build|run]", "ابزار دات‌نت", ".NET CLI", CommandCategory.Custom, "dotnet build", ["dotnet"]),
        new("npm", "npm [install|run]", "مدیر بسته نود", "Node package manager", CommandCategory.Custom, "npm run dev", ["npm", "npx"]),
        new("python", "python <script>", "اجرای پایتون", "Run python", CommandCategory.Custom, "python app.py", ["py", "python"]),
        new("docker", "docker ps", "مدیریت کانتینر", "Containers", CommandCategory.Custom, "docker ps -a", ["docker"]),
        new("|", "cmd1 | cmd2", "پایپ: خروجی به ورودی بعدی", "Pipe output", CommandCategory.Bash, "git log --oneline | head -20", ["pipe"]),
        new(">", "cmd > file", "هدایت خروجی به فایل", "Redirect output", CommandCategory.Bash, "ls -la > list.txt", ["redirect"]),
    }.AsReadOnly();

    public static GitCommandInfo? Find(string name)
        => All.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            || c.AliasesValues.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase)));
}
