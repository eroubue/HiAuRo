import { defineComponent, h } from 'vue';

export const ItemsSickle = defineComponent({
  name: 'ItemsSickle',
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
        h('path', {"d": "M5.49951 7.80078C5.49932 8.18707 5.18657 8.49976 4.80029 8.5C4.41381 8.5 4.10029 8.18722 4.1001 7.80078V6.5C4.10031 4.20048 5.80029 4.63259 5.80029 3.5C5.80029 3.10341 5.29995 2.79989 4.6001 2.7998C3.4001 2.7998 2.80029 4.29689 2.80029 3C2.80064 2.20018 3.80046 1.5 4.80029 1.5C6.2998 1.50021 7.19945 2.50024 7.19971 3.5C7.19971 4.99986 5.49983 5.00042 5.49951 6.5V7.80078Z", "fillRule": "evenodd"})
      ]
    );
  }
});
