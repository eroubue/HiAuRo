import { defineComponent, h } from 'vue';

export const ItemsPotion3 = defineComponent({
  name: 'ItemsPotion3',
  props: {
    class: {
      type: String,
      default: ''
    }
  },
  setup(props, { attrs }) {
    return () => h(
      'svg',
      {
        viewBox: '0 0 20 20',
        
        class: `game-icons ${props.class}`,
        ...attrs
      },
      [
        h('path', {"d": "M4.20001 3.50919C4.20001 3.47275 4.17996 3.43927 4.14783 3.42207C3.93368 3.30738 3.8 3.08416 3.8 2.84123V2.75326C3.8 2.44174 4.05254 2.18921 4.36405 2.18921H5.63595C5.94747 2.18921 6.2 2.44174 6.2 2.75326V2.84124C6.2 3.08416 6.06633 3.30738 5.85218 3.42207C5.82006 3.43927 5.80001 3.47275 5.80001 3.50919V7.0108C5.80001 7.45262 5.44184 7.8108 5.00001 7.8108C4.55818 7.8108 4.20001 7.45262 4.20001 7.0108V3.50919Z", "fillRule": "evenodd"})
      ]
    );
  }
});
