import { VectorTile } from '@mapbox/vector-tile';
import Protobuf from 'pbf';

const requests = [];
chrome.storage.local.set({ 'requestsLog': requests });

const requestedUrls = [];

chrome.webRequest.onCompleted.addListener(
    details => {
        const url = details.url;

        chrome.storage.local.get(['url'],
            result => {

                if (typeof result.url == 'undefined') {
                    console.log('Url is undefined');
                }
                else{
                    let includedUrl = result.url[0];

                    if (!requestedUrls.includes(url) &&
                        url.includes(includedUrl)) {
                        requestedUrls.push(url);

                        fetch(url)
                            .then(response => response.arrayBuffer())
                            .then(data => {
                                const pbf = new Protobuf(data);
                                const tile = new VectorTile(pbf);
                                let params = (new URL(url)).searchParams;
                                const x = params.get('x');
                                const y = params.get('y');
                                const z = params.get('z');
                                requests.push(`${requests.length + 1}) z=${z} x=${x} y=${y} </br> Number of objects: ${Object.values(tile.layers)[0].length}`);
                                chrome.storage.local.set({ 'requestsLog': requests });
                            });
                    }
                }
            });
    },
    { urls: ['<all_urls>'] },
    ['extraHeaders']
);