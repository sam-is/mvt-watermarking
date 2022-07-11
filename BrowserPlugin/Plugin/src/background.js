import { VectorTile } from '@mapbox/vector-tile';
import Protobuf from 'pbf';

const requests = [];
chrome.storage.local.set({ 'requestsLog': requests });

const requestedUrls = [];



chrome.webRequest.onCompleted.addListener(
    details => {
        const url = details.url;
        if (!requestedUrls.includes(url) &&
            url.includes('https://functions.yandexcloud.net/d4esngj8sq9q03pu1f39')) {
            requestedUrls.push(url);

            fetch(url)
                .then(response => response.arrayBuffer())
                .then(data => {
                    const pbf = new Protobuf(data);
                    const tile = new VectorTile(pbf);
                    let params = (new URL(url)).searchParams;
                    const x = params.get("x");
                    const y = params.get("y");
                    const z = params.get("z");
                    requests.push(`${requests.length + 1}) z=${z} x=${x} y=${y} </br> Count object: ${Object.values(tile.layers)[0].length}`);
                    chrome.storage.local.set({ 'requestsLog': requests });
                });
        }
    },
    { urls: ['<all_urls>'] },
    ['extraHeaders']
);