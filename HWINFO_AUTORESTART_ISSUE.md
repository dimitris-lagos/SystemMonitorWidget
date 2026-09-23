# HWiNFO Autorestart Issue

## Τι παρατηρήθηκε

- Το SystemMonitorWidget και το HWiNFO ξεκίνησαν στις **08:22:51**.
- Στο ενεργό config υπήρχαν LaunchHWiNFO=True και AutoRestartHWiNFO=True.
- Το widget έχει όριο επανεκκίνησης **11 ώρες και 30 λεπτά**, οπότε έπρεπε να κάνει restart περίπου στις **19:52**.
- Το HWiNFO παρέμεινε το αρχικό process και το shared memory έπαψε να είναι διαθέσιμο μετά το όριο των περίπου 12 ωρών.
- Το HWiNFO επανεκκινήθηκε τελικά χειροκίνητα και το shared memory επανήλθε.

## Πιθανή αιτία

Το widget πιθανότατα δεν μπόρεσε να κλείσει ή να επανεκκινήσει το elevated HWiNFO λόγω διαφορετικών δικαιωμάτων. Ο κώδικας καταπίνει τα σχετικά exceptions και εμφανίζει μόνο προσωρινό status, χωρίς μόνιμο log, οπότε δεν έμεινε η ακριβής αιτία της αποτυχίας.

## Τι χρειάζεται

- Μόνιμο log για κάθε απόπειρα autorestart και το πλήρες exception.
- Έλεγχος ύπαρξης του Global\HWiNFO_SENS_SM2, πέρα από το process uptime.
- Restart μέσω elevated helper ή προγραμματισμένης εργασίας με τα σωστά δικαιώματα.
- Επιβεβαίωση μετά το restart ότι το νέο process ξεκίνησε και το shared memory έγινε ξανά διαθέσιμο.

## Υλοποίηση

- Προστέθηκε ξεχωριστό `SystemMonitorWidget.HWiNFORestartHelper.exe` με administrator manifest.
- Και οι δύο administrator helpers, μαζί με το OpenHardwareMonitor dependency του fan helper, είναι embedded στο κύριο EXE και εξάγονται με έλεγχο SHA-256 στο `%LOCALAPPDATA%\SystemMonitorWidget\Runtime\<version>`.
- Το widget κρατά αναλυτικό log στο `%LOCALAPPDATA%\SystemMonitorWidget\HWiNFO-autorestart.log` και δεν καταπίνει πλέον τις αποτυχίες του uptime/restart.
- Το autorestart ενεργοποιείται στις 11 ώρες και 30 λεπτά uptime, όταν δεν τρέχει HWiNFO, ή όταν το shared memory λείπει συνεχόμενα για δύο λεπτά.
- Ο helper τερματίζει όλα τα HWiNFO32/64 processes, ξεκινά το ρυθμισμένο executable και περιμένει έως 45 δευτερόλεπτα για νέο process και ενεργό `Global\HWiNFO_SENS_SM2`.
- Το widget κάνει δεύτερη, ανεξάρτητη επιβεβαίωση πριν δηλώσει επιτυχημένο restart.

## Πραγματική δοκιμή

- Έγινε restart του elevated HWiNFO μέσω του νέου helper.
- Επιβεβαιώθηκε διαφορετικό process ID μετά το restart.
- Ο helper επιβεβαίωσε την επαναφορά του shared memory.
- Ανεξάρτητος reader διάβασε ξανά 309 live readings και το νέο process παρέμεινε ενεργό.
