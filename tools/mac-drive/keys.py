"""Turns one test-settings value into AppleScript key codes (US ANSI). Reads the JSON itself; prints only AppleScript to stdout for osascript's stdin."""
import json, sys

base = {'a':0,'s':1,'d':2,'f':3,'h':4,'g':5,'z':6,'x':7,'c':8,'v':9,'b':11,'q':12,'w':13,'e':14,'r':15,'y':16,'t':17,
        '1':18,'2':19,'3':20,'4':21,'6':22,'5':23,'=':24,'9':25,'7':26,'-':27,'8':28,'0':29,']':30,'o':31,'u':32,'[':33,
        'i':34,'p':35,'l':37,'j':38,"'":39,'k':40,';':41,'\\':42,',':43,'/':44,'n':45,'m':46,'.':47,' ':49,'`':50}
shifted = {'!':'1','@':'2','#':'3','$':'4','%':'5','^':'6','&':'7','*':'8','(':'9',')':'0','_':'-','+':'=',
           '{':'[','}':']','|':'\\',':':';','"':"'",'<':',','>':'.','?':'/','~':'`'}

value = json.load(open(sys.argv[1]))["TestUsers"][sys.argv[2]][sys.argv[3]]
out = ['tell application "System Events"', 'tell process "TrStudio"']
for ch in value:
    if ch in base:
        out.append(f'key code {base[ch]}')
    elif ch.lower() in base and ch.isupper():
        out.append(f'key code {base[ch.lower()]} using shift down')
    elif ch in shifted:
        out.append(f'key code {base[shifted[ch]]} using shift down')
    else:
        sys.stderr.write('a character has no key code; stopping\n'); sys.exit(2)
    out.append('delay 0.02')
out += ['end tell', 'end tell']
print('\n'.join(out))
