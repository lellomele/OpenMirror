"""Build portable upstream receivers using an isolated, checksummed MSYS2 sysroot.

No package manager installation or machine PATH modification is performed.
Build tools come from an existing MSYS2 MINGW64 toolchain. Runtime packages are
downloaded and extracted only under --work. HTTPS and repository SHA256 protect
indexed packages. For archive-only packages the generated manifest pins SHA256.
"""
import argparse, concurrent.futures, hashlib, json, os, pathlib, re, shutil, subprocess, sys, tarfile, urllib.request, zipfile

REPO = 'https://repo.msys2.org/mingw/mingw64/'
SOURCE_REPO = 'https://repo.msys2.org/mingw/sources/'
PREFIX = 'mingw-w64-x86_64-'
SCRCPY_VERSION = '5.0.1'
UXPLAY_COMMIT = 'dbb8a8f451597aa5f2e56c830ce41c4a655a8f98'
ROOTS = ['libplist', 'openssl', 'gstreamer', 'gst-plugins-base', 'gst-plugins-good', 'gst-plugins-bad', 'gst-libav', 'ffmpeg']

def request(url):
    return urllib.request.urlopen(urllib.request.Request(url, headers={'User-Agent': 'OpenMirror-build/0.1'}), timeout=120)

def sha(path):
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        for block in iter(lambda: f.read(1024*1024), b''): h.update(block)
    return h.hexdigest()

def download(url, target, expected=None):
    target = pathlib.Path(target)
    target.parent.mkdir(parents=True, exist_ok=True)
    if target.exists() and (not expected or sha(target) == expected): return target
    print('Download', target.name, flush=True)
    temporary = target.with_suffix(target.suffix + '.part')
    for attempt in range(3):
        try:
            with request(url) as src, open(temporary, 'wb') as dest: shutil.copyfileobj(src, dest)
            if expected and sha(temporary) != expected: raise RuntimeError('SHA256 mismatch: ' + str(target))
            temporary.replace(target)
            return target
        except Exception:
            if attempt == 2: raise

def run(cmd, **kwargs):
    print('Run', ' '.join(map(str, cmd)), flush=True)
    return subprocess.run(list(map(str,cmd)), check=True, **kwargs)

def parse_desc(raw):
    out = {}
    for match in re.finditer(r'%([^%\r\n]+)%\r?\n(.*?)(?=\r?\n\r?\n|\Z)', raw, re.S):
        out[match.group(1)] = match.group(2).splitlines()
    return out

def version_key(name):
    return [int(p) if p.isdigit() else p for p in re.split(r'(\d+)', name)]

class Builder:
    def __init__(self,args):
        self.args=args; self.app=pathlib.Path(args.app).resolve(); self.work=pathlib.Path(args.work).resolve(); self.msys=pathlib.Path(args.msys).resolve()
        self.cache=self.work/'cache'; self.db=self.work/'db'; self.sysroot=self.work/'sysroot'; self.prefix=self.sysroot/'mingw64'; self.sources=self.app/'third_party'; self.tar=self.msys/'usr/bin/bsdtar.exe'
        self.env=os.environ.copy(); self.env['PATH']=str(self.prefix/'bin')+';'+str(self.msys/'mingw64/bin')+';'+str(self.msys/'usr/bin')+';'+self.env.get('PATH','')
        self.env['PKG_CONFIG_LIBDIR']=str(self.prefix/'lib/pkgconfig')+';'+str(self.prefix/'share/pkgconfig')
        self.env['PKG_CONFIG_PATH']=self.env['PKG_CONFIG_LIBDIR']; self.packages={}; self.providers={}; self.selected={}; self.shipped=set(); self.import_cache={}
        self.work.mkdir(parents=True, exist_ok=True); self.sources.mkdir(parents=True, exist_ok=True)

    def extract(self,archive,target):
        target.mkdir(parents=True,exist_ok=True)
        run([self.tar,'-xf',archive,'-C',target])

    def load_database(self):
        archive=download(REPO+'mingw64.db',self.cache/'mingw64.db')
        if not self.db.exists(): self.extract(archive,self.db)
        for path in self.db.glob('*/desc'):
            p=parse_desc(path.read_text(encoding='utf-8'))
            self.packages[p['NAME'][0]]=p
            for name in p.get('PROVIDES',[]): self.providers[re.split('[<>=]',name)[0]]=p['NAME'][0]
        index=download(REPO,self.cache/'index.html')
        self.archives=re.findall(r'href="([^"]+\.pkg\.tar\.zst)"',index.read_text(encoding='utf-8'))

    def package(self,name):
        name=re.split('[<>=]',name)[0]
        if name in self.selected: return
        if name not in self.packages and name in self.providers: name=self.providers[name]
        if name in self.selected: return
        p=self.packages.get(name)
        if p:
            archive=download(REPO+p['FILENAME'][0],self.cache/p['FILENAME'][0],p['SHA256SUM'][0])
        else:
            # Some rolling-repository packages temporarily disappear from the DB;
            # retain their official archived build and exact content identity.
            pattern=re.compile(re.escape(name)+r'-\d[^/]*-any\.pkg\.tar\.zst$')
            matches=sorted([f for f in self.archives if pattern.fullmatch(f)],key=version_key)
            if not matches: raise RuntimeError('Unresolved dependency: '+name)
            archive=download(REPO+matches[-1],self.cache/matches[-1])
            raw=subprocess.check_output([str(self.tar),'-xOf',str(archive),'.PKGINFO'],text=True,encoding='utf-8')
            info={}
            for line in raw.splitlines():
                if ' = ' in line:
                    key,value=line.split(' = ',1); info.setdefault(key,[]).append(value)
            p={'NAME':[name],'BASE':info.get('pkgbase',[name]),'VERSION':info['pkgver'],'DEPENDS':info.get('depend',[]),'FILENAME':[archive.name],'LICENSE':info.get('license',[]),'SHA256SUM':[sha(archive)]}
        self.selected[name]=p
        marker=self.work/'extracted'/archive.name
        if not marker.exists():
            self.extract(archive,self.sysroot); marker.parent.mkdir(parents=True,exist_ok=True); marker.write_text(sha(archive))
        for dep in p.get('DEPENDS',[]): self.package(dep)

    def save_manifest(self):
        data=[{'name':n,'version':p['VERSION'][0],'file':p['FILENAME'][0],'sha256':p['SHA256SUM'][0],'license':p.get('LICENSE',[]),'source_package':p.get('BASE',[n])[0]+'-'+p['VERSION'][0]+'.src.tar.zst','url':REPO+p['FILENAME'][0]} for n,p in sorted(self.selected.items())]
        (self.sources/'msys2-packages.json').write_text(json.dumps(data,indent=2),encoding='utf-8')

    def obtain_sources(self):
        # MSYS2 source packages include the upstream source archives, PKGBUILD
        # and patches. Retain exact versions for packages owning shipped files.
        dest=self.sources/'msys2-sources'
        owners={}
        for n,p in self.selected.items():
            archive=self.cache/p['FILENAME'][0]
            files=set(subprocess.check_output([str(self.tar),'-tf',str(archive)],text=True,encoding='utf-8',errors='replace').splitlines())
            owned=sorted(self.shipped & files)
            if owned: owners[n]={'files':owned,'package':p}
        (self.sources/'runtime-source-map.json').write_text(json.dumps(owners,indent=2),encoding='utf-8')
        unique={p['package'].get('BASE',p['package']['NAME'])[0]:p['package'] for p in owners.values()}
        def get(p):
            name=p.get('BASE',p['NAME'])[0]+'-'+p['VERSION'][0]+'.src.tar.zst'
            download(SOURCE_REPO+name,dest/name)
        with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool: list(pool.map(get,unique.values()))
        source_manifest=[{'file':p.name,'sha256':sha(p),'url':SOURCE_REPO+p.name} for p in sorted(dest.glob('*.src.tar.zst'))]
        (self.sources/'source-packages.json').write_text(json.dumps(source_manifest,indent=2),encoding='utf-8')

    def build(self):
        self.load_database()
        for name in ROOTS: self.package(PREFIX+name)
        self.save_manifest()
        upstream=self.work/'uxplay'
        if not upstream.exists():
            archive=download('https://codeload.github.com/FDH2/UxPlay/tar.gz/'+UXPLAY_COMMIT,self.cache/('uxplay-'+UXPLAY_COMMIT+'.tar.gz'))
            upstream.mkdir(parents=True,exist_ok=True)
            run([self.tar,'-xf',archive,'-C',upstream,'--strip-components=1'])
        source_dest=self.sources/'UxPlay'
        if not source_dest.exists(): shutil.copytree(upstream,source_dest,ignore=shutil.ignore_patterns('.git','build'))
        (self.sources/'UxPlay-COMMIT.txt').write_text(UXPLAY_COMMIT+'\nhttps://github.com/FDH2/UxPlay/tree/'+UXPLAY_COMMIT+'\n',encoding='utf-8')
        build=self.work/'uxplay-build'
        cmake=self.msys/'mingw64/bin/cmake.exe'
        run([cmake,'-S',upstream,'-B',build,'-G','Ninja','-DCMAKE_BUILD_TYPE=Release','-DNO_MARCH_NATIVE=ON','-DUSE_DNS_SD=OFF','-DCMAKE_PREFIX_PATH='+str(self.prefix),'-DCMAKE_C_COMPILER='+str(self.msys/'mingw64/bin/gcc.exe'),'-DCMAKE_CXX_COMPILER='+str(self.msys/'mingw64/bin/g++.exe'),'-DPKG_CONFIG_EXECUTABLE='+str(self.msys/'mingw64/bin/pkg-config.exe')],env=self.env)
        run([cmake,'--build',build,'--parallel','4'],env=self.env)
        self.scrcpy()
        self.package_outputs()
        if not self.args.skip_sources: self.obtain_sources()

    def scrcpy(self):
        name='scrcpy-win64-v'+SCRCPY_VERSION+'.zip'
        base='https://github.com/Genymobile/scrcpy/releases/download/v'+SCRCPY_VERSION+'/'
        sums=download(base+'SHA256SUMS.txt',self.cache/'scrcpy-SHA256SUMS.txt')
        match=re.search(r'^([a-f0-9]{64})\s+\*?'+re.escape(name)+r'$',sums.read_text(),re.M)
        if not match: raise RuntimeError('Missing scrcpy upstream checksum')
        archive=download(base+name,self.cache/name,match.group(1))
        target=self.app/'bin/android'; target.mkdir(parents=True,exist_ok=True)
        with zipfile.ZipFile(archive) as z:
            for item in z.infolist():
                if item.is_dir(): continue
                dest=target/pathlib.PurePosixPath(item.filename).name
                with z.open(item) as src,open(dest,'wb') as dst: shutil.copyfileobj(src,dst)
        source=download('https://codeload.github.com/Genymobile/scrcpy/tar.gz/refs/tags/v'+SCRCPY_VERSION,self.sources/('scrcpy-v'+SCRCPY_VERSION+'.tar.gz'))
        shutil.copy2(sums,self.sources/'scrcpy-SHA256SUMS.txt')
        dependency_manifest=[]
        with tarfile.open(source,'r:gz') as tree:
            for dep in ['ffmpeg','sdl','libusb','dav1d','adb_windows']:
                script=tree.extractfile('scrcpy-'+SCRCPY_VERSION+'/app/deps/'+dep+'.sh').read().decode()
                version=re.search(r'^VERSION=(.+)$',script,re.M).group(1).strip('"')
                url=re.search(r'^URL="(.+)"$',script,re.M).group(1).replace('$VERSION',version).replace('${VERSION}',version)
                expected=re.search(r'^SHA256SUM=([a-f0-9]{64})$',script,re.M).group(1)
                suffix=next(s for s in ['.tar.xz','.tar.gz','.zip'] if url.endswith(s))
                filename=dep+'-'+version+suffix
                dest=self.sources/'scrcpy-dependencies'/filename
                if dep=='adb_windows':
                    dest=self.cache/filename
                download(url,dest,expected)
                dependency_manifest.append({'name':dep,'version':version,'url':url,'sha256':expected})
                if dep=='adb_windows':
                    with zipfile.ZipFile(dest) as z:
                        for item in z.infolist():
                            if pathlib.PurePosixPath(item.filename).name.lower().startswith(('notice','license')) and not item.is_dir():
                                license_dest=self.sources/'scrcpy-dependencies'/('platform-tools-'+pathlib.PurePosixPath(item.filename).name)
                                license_dest.parent.mkdir(parents=True,exist_ok=True)
                                license_dest.write_bytes(z.read(item))
        (self.sources/'scrcpy-dependencies.json').write_text(json.dumps(dependency_manifest,indent=2),encoding='utf-8')
        notices=self.sources/'runtime-licenses/scrcpy-dependencies'
        notices.mkdir(parents=True,exist_ok=True)
        for archive in (self.sources/'scrcpy-dependencies').glob('*.tar.*'):
            with tarfile.open(archive,'r:*') as tree:
                for member in tree.getmembers():
                    if member.isfile() and len(pathlib.PurePosixPath(member.name).parts)==2 and re.match(r'^(COPYING|LICENSE|NOTICE)',pathlib.PurePosixPath(member.name).name,re.I):
                        (notices/(archive.name+'-'+pathlib.PurePosixPath(member.name).name)).write_bytes(tree.extractfile(member).read())

    def copy_closure(self,seeds,target):
        target.mkdir(parents=True,exist_ok=True)
        files={p.name.lower():p for p in (self.prefix/'bin').glob('*') if p.is_file()}
        files.update({p.name.lower():p for p in (self.msys/'mingw64/bin').glob('*.dll') if p.name.lower() not in files})
        done=set(); queue=list(seeds); objdump=self.msys/'mingw64/bin/objdump.exe'
        while queue:
            src=pathlib.Path(queue.pop()); key=src.name.lower()
            if key in done: continue
            done.add(key)
            destination=target/src.name
            if not destination.exists() or sha(src)!=sha(destination): shutil.copy2(src,destination)
            try: self.shipped.add('mingw64/'+src.relative_to(self.prefix).as_posix())
            except ValueError: pass
            cache_key=str(src)
            if cache_key not in self.import_cache:
                raw=subprocess.check_output([str(objdump),'-p',str(src)],text=True,encoding='utf-8',errors='replace')
                self.import_cache[cache_key]=re.findall(r'DLL Name:\s*(\S+)',raw)
            for name in self.import_cache[cache_key]:
                dep=files.get(name.lower())
                if dep: queue.append(dep)
                elif not (pathlib.Path(os.environ.get('SystemRoot','C:/Windows'))/'System32'/name).exists() and not name.lower().startswith(('api-ms-','ext-ms-')):
                    raise RuntimeError('Unresolved portable runtime DLL: '+name+' for '+str(src))

    def package_outputs(self):
        air=self.app/'bin/airplay'; air.mkdir(parents=True,exist_ok=True)
        self.copy_closure([self.work/'uxplay-build/uxplay.exe'],air)
        plugins=['coreelements','typefindfunctions','app','playback','autodetect','audioconvert','audioresample','volume','level','videoconvertscale','isomp4','rtpmanager','rtp','udp','tcp','videoparsersbad','libav','d3d11','wasapi','audioparsers','imagefreeze','pango','jpeg','png','opus']
        pdest=air/'lib/gstreamer-1.0'; pdest.mkdir(parents=True,exist_ok=True)
        for name in plugins:
            src=self.prefix/'lib/gstreamer-1.0'/('libgst'+name+'.dll')
            if not src.exists(): raise RuntimeError('Required GStreamer plugin absent: '+str(src))
            self.copy_closure([src],air)
            shutil.move(air/src.name,pdest/src.name)
        scanner=self.prefix/'libexec/gstreamer-1.0/gst-plugin-scanner.exe'
        if scanner.exists():
            self.copy_closure([scanner],air)
        self.copy_closure([self.prefix/'bin/gst-inspect-1.0.exe',self.prefix/'bin/gst-launch-1.0.exe'],air)
        self.copy_closure([self.prefix/'bin/ffmpeg.exe',self.prefix/'bin/ffprobe.exe'],self.app/'bin/ffmpeg')
        licenses=self.sources/'runtime-licenses'
        if (self.prefix/'share/licenses').exists(): shutil.copytree(self.prefix/'share/licenses',licenses,dirs_exist_ok=True)
        (self.sources/'BACKENDS.txt').write_text('UxPlay: GPL-3.0-or-later, source tree in UxPlay.\nScrcpy '+SCRCPY_VERSION+': Apache-2.0; exact source archive included. Android platform-tools and bundled FFmpeg/SDL have their own licenses.\nGStreamer / libplist / FFmpeg / supporting DLLs: see runtime-licenses and msys2-packages.json.\nmsys2-sources contains the exact source packages (upstream archives, build recipes and patches) for the distributed MSYS2 runtime libraries; runtime-source-map.json identifies ownership.\n',encoding='utf-8')
        print('Portable backends packaged at',self.app/'bin',flush=True)

if __name__=='__main__':
    ap=argparse.ArgumentParser(); ap.add_argument('action',choices=['build','package']); ap.add_argument('--app',required=True); ap.add_argument('--work',required=True); ap.add_argument('--msys',required=True); ap.add_argument('--skip-sources',action='store_true')
    args=ap.parse_args(); builder=Builder(args)
    if args.action=='build': builder.build()
    else: builder.package_outputs()
